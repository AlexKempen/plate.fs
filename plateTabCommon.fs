FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

/**
 * A module for creating plate tabs. Used by the plateTab feature to create standalone tabs, and by the plateHole feature
 * to create tabs which expand existing plates.
 */

export enum TabGeometryType
{
    POINT,
    DIRECTION,
    HOLE
}

export predicate tabEndsPredicate(definition is map)
{
    annotation { "Name" : "Enable tab start and end" }
    definition.enableTabEnds is boolean;

    if (definition.enableTabEnds)
    {
        annotation { "Name" : "Start point or hole",
                    "Filter" : EntityType.VERTEX || GeometryType.CYLINDER,
                    /* || GeometryType.LINE || GeometryType.CIRCLE || GeometryType.ARC || GeometryType.CYLINDER || (GeometryType.PLANE && ConstructionObject.YES)*/
                    "MaxNumberOfPicks" : 1 }
        definition.tabStart is Query;

        annotation { "Name" : "End point or hole",
                    "Filter" : EntityType.VERTEX || GeometryType.CYLINDER,
                    /* || GeometryType.LINE || GeometryType.CIRCLE || GeometryType.ARC || GeometryType.CYLINDER || (GeometryType.PLANE && ConstructionObject.YES)*/
                    "MaxNumberOfPicks" : 1 }
        definition.tabEnd is Query;
    }
}

/**
 * Extrudes a solid tab from a given tabDefinition. Used by the Plate Tab feature and the Plate Hole feature.
 * @param id : @autocomplete `id + "plateTab"`
 * @param tabDefinition {{
 *      @field platePlane {Plane} :
 *              The plane of the tab.
 *              @autocomplete `platePlane`
 *      @field depth {ValueWithUnits} :
 *              The depth of the tab.
 *              @autocomplete `depth`
 *      @field enableTabEnds {boolean} :
 *              Whether the tab has a start and/or end. If false, the tab is fully connected.
 *              @autocomplete `definition.enableTabEnds`
 *      @field tabStart {Query} :
 *              A query for the start entity of the tab. Cannot be a direction if tab scope is empty.
 *              @autocomplete `definition.tabStart`
 *      @field tabEnd {Query} :
 *              A query for the end entity of the tab. Cannot be a direction if tab scope is empty.
 *              @autocomplete `definition.tabEnd`
 *      @field holeMap {HoleMap} :
 *              The hole map of holes being used to define the tab.
 *              @autocomplete `newHoleMap`
 *      @field entitiesToCollideWith {Query} :
 *              A query for entities to collide tab ends with.
 *              @autocomplete `definition.boundingEntities`
 *      @field booleanToPlate {boolean} : @optional
 *              Whether to boolean the tab with an existing plate body.
 *              @autocomplete `true`
 *      @field plate {Query} : @requiredif booleanToPlate == `true`
 *              The query for the plate to boolean to.
 *              @autocomplete `plateAttribute.query`
 *      @field extrudeId {id} : @requiredif booleanToPlate == `true`
 *              The extrudeId of the plate to boolean to.
 *              @autocomplete `plateAttribute.extrudeId`
 *      @field centroid {Vector} : @requiredif booleanToPlate == `true`
 *              A 2D vector representing the center of the tab (or the center of the plate).
 *              @autocomplete `centroid`
 *      @field existingHoleMap {HoleMap} : @optional
 *              The hole map of holes already on the plate.
 *              @autocomplete `plateAttribute.holeMap`
 * }}
 */
export function createPlateTab(context is Context, id is Id, tabDefinition is map, toDelete is box)
{
    tabDefinition = mergeMaps({ "booleanToPlate" : false, "existingHoleMap" : {} as HoleMap }, tabDefinition);

    if (tabDefinition.booleanToPlate)
    {
        tabDefinition.entitiesToCollideWith = qUnion([tabDefinition.entitiesToCollideWith, tabDefinition.plate]);
    }

    tabDefinition.calculatedLength = calculateLength(context, qEverything(EntityType.BODY));

    var tabGeometryArray = getTabGeometryArray(context, tabDefinition);
    tabGeometryArray = addTabEnds(context, tabDefinition, tabGeometryArray);

    var chirality = getChirality(context, tabDefinition, tabGeometryArray);

    if (tabDefinition.enableTabEnds)
    {
        tabGeometryArray = convertTabDirectionsToPoints(context, tabDefinition, tabGeometryArray, chirality);
    }

    const sketchGeometryId = id + "sketch";
    drawTabGeometry(context, sketchGeometryId, tabDefinition, tabGeometryArray, chirality);
    addDebugEntities(context, qCreatedBy(sketchGeometryId, EntityType.BODY)->qBodyType(BodyType.WIRE), DebugColor.BLUE);
    toDelete[] = append(toDelete[], qCreatedBy(sketchGeometryId, EntityType.BODY));

    const tabGeometryId = id + "tabGeometry";
    const tabFaces = getTabFaces(context, tabGeometryId, tabDefinition, tabGeometryArray, sketchGeometryId);
    toDelete[] = append(toDelete[], qCreatedBy(tabGeometryId, EntityType.BODY));

    try
    {
        opExtrude(context, id + "tabExtrude", {
                    "entities" : qUnion(tabFaces),
                    "direction" : tabDefinition.platePlane.normal,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : tabDefinition.depth
                });
    }
    catch
    {
        throw regenError(plateError(PlateError.TAB_EXTRUDE_FAILED), qUnion(tabFaces));
    }

    if (tabDefinition.booleanToPlate)
    {
        try
        {
            opBoolean(context, id + "tabBoolean", {
                        "tools" : qUnion([tabDefinition.plate, qCreatedBy(id + "tabExtrude", EntityType.BODY)]),
                        "operationType" : BooleanOperationType.UNION
                    });
        }
        catch
        {
            throw regenError(plateError(PlateError.TAB_BOOLEAN_FAILED), qCreatedBy(id + "tabExtrude", EntityType.BODY));
        }
    }
}

function getTabGeometryArray(context is Context, tabDefinition is map) returns array
{
    var tabGeometryArray = [];
    for (var point, value in tabDefinition.holeMap)
    {
        tabGeometryArray = append(tabGeometryArray, {
                    "query" : value.query,
                    "point" : point,
                    "outerRadius" : value.outerRadius,
                    "type" : TabGeometryType.HOLE });
    }
    return tabGeometryArray;
}

function getChirality(context is Context, tabDefinition is map, tabGeometryArray is array)
{
    if (tabDefinition.centroid == undefined)
    {
        var points = vector([0 * meter, 0 * meter]);
        for (var tabGeometry in tabGeometryArray)
        {
            points += (tabGeometry["type"] == TabGeometryType.DIRECTION ? tabGeometry.arbitraryPoint : tabGeometry.point);
        }
        tabDefinition.centroid = points / size(tabGeometryArray);
    }

    var angleSum = 0 * radian;
    for (var i = 0; i < size(tabGeometryArray) - (tabDefinition.booleanToPlate && tabDefinition.enableTabEnds ? 1 : 0); i += 1)
    {
        const tabGeometry = tabGeometryArray[i];
        const next = getNext(tabGeometryArray, i);

        const point1 = (tabGeometry["type"] == TabGeometryType.DIRECTION ? tabGeometry.arbitraryPoint : tabGeometry.point);
        const point2 = (next["type"] == TabGeometryType.DIRECTION ? next.arbitraryPoint : next.point);

        const vector1 = point1 - tabDefinition.centroid;
        const vector2 = point2 - tabDefinition.centroid;
        var angle = atan2(vector2[1], vector2[0]) - atan2(vector1[1], vector1[0]);
        if (angle > PI * radian)
            angle -= 2 * PI * radian;
        else if (angle <= -PI * radian)
            angle += 2 * PI * radian;
        angleSum += angle;
    }
    return angleSum >= 0 * radian;
}

function hasTwoDirections(tabGeometryArray is array)
{
    return tabGeometryArray[0]["type"] == TabGeometryType.DIRECTION && tabGeometryArray[size(tabGeometryArray) - 1]["type"] == TabGeometryType.DIRECTION;
}

function convertTabDirectionsToPoints(context is Context, tabDefinition is map, tabGeometryArray is array, chirality is boolean) returns array
{
    for (var i in [0, size(tabGeometryArray) - 1])
    {
        if (tabGeometryArray[i]["type"] == TabGeometryType.DIRECTION)
        {
            tabGeometryArray[i].point = tryRaycast(context, tabDefinition,
                i == 0 ? tabGeometryArray[i] : getPrevious(tabGeometryArray, i),
                i == 0 ? getNext(tabGeometryArray, i) : tabGeometryArray[i], chirality);

            tabGeometryArray[i]["type"] = TabGeometryType.POINT;
        }
    }
    return tabGeometryArray;
}

function tryRaycast(context is Context, tabDefinition is map, tabGeometry is map, next is map, chirality is boolean) returns array
{
    var startPoint;
    var tabGeometryToUse;

    if (tabGeometry["type"] == TabGeometryType.DIRECTION && next["type"] == TabGeometryType.HOLE)
    {
        const offsetPoint = next.point + worldToPlane(tabDefinition.platePlane, tabGeometry.direction * meter)->normalize() * tabDefinition.calculatedLength;
        startPoint = circleToCircle(next.point, next.outerRadius, offsetPoint, next.outerRadius, chirality)[0];
        tabGeometryToUse = tabGeometry;
    }
    else if (tabGeometry["type"] == TabGeometryType.HOLE && next["type"] == TabGeometryType.DIRECTION)
    {
        const offsetPoint = tabGeometry.point + worldToPlane(tabDefinition.platePlane, next.direction * meter)->normalize() * tabDefinition.calculatedLength;
        const startPoint = circleToCircle(tabGeometry.point, tabGeometry.outerRadius, offsetPoint, tabGeometry.outerRadius, chirality)[0];
        tabGeometryToUse = next;
    }
    // Eventually will need to handle additional situations, i.e. direction to hole
    return getClosestRaycast(context, tabDefinition, tabGeometryToUse, startPoint);
}

function drawTabGeometry(context is Context, id is Id, tabDefinition is map, tabGeometryArray is array, chirality is boolean)
{
    const holeId = id + "hole";
    for (var i, tabGeometry in tabGeometryArray)
    {
        if (tabGeometry["type"] == TabGeometryType.HOLE)
        {
            setExternalDisambiguation(context, holeId + unstableIdComponent(i), tabGeometry.query);
            const holeSketch = newSketchOnPlane(context, holeId + unstableIdComponent(i) + "holeSketch", {
                        "sketchPlane" : tabDefinition.platePlane
                    });

            skCircle(holeSketch, "hole", {
                        "center" : tabGeometry.point,
                        "radius" : tabGeometry.outerRadius
                    });

            skSolve(holeSketch);
        }
    }

    var loopSize = size(tabGeometryArray);
    if (loopSize <= 1)
    {
        return;
    }

    // if ends are enabled, don't loop through entire array (prevents plate from self-connecting)
    if (tabDefinition.booleanToPlate && tabDefinition.enableTabEnds)
    {
        loopSize -= 1;
    }

    const lineId = id + "line";
    for (var i = 0; i < loopSize; i += 1)
    {
        const tabGeometry = tabGeometryArray[i];
        const next = getNext(tabGeometryArray, i);

        var linePoints;
        if (tabGeometry["type"] == TabGeometryType.POINT)
        {
            if (next["type"] == TabGeometryType.HOLE)
            {
                linePoints = pointToCircle(tabGeometry.point, next.point, next.outerRadius, chirality);
            }
            else if (next["type"] == TabGeometryType.POINT)
            {
                linePoints = [tabGeometry.point, next.point];
            }
        }
        else if (tabGeometry["type"] == TabGeometryType.HOLE)
        {
            if (next["type"] == TabGeometryType.HOLE)
            {
                linePoints = circleToCircle(tabGeometry.point, tabGeometry.outerRadius, next.point, next.outerRadius, chirality);
            }
            else if (next["type"] == TabGeometryType.POINT)
            {
                linePoints = pointToCircle(tabGeometry.point, tabGeometry.outerRadius, next.point, chirality);
            }
        }

        if (linePoints == undefined)
        {
            reportFeatureInfo(context, id, plateError(PlateError.TAB_GEOMETRY_FAILED));
            continue;
        }

        if (tolerantEquals(linePoints[0], linePoints[1]))
        {
            continue;
        }

        for (var i = 0; i < size(linePoints); i += 1)
        {
            linePoints[i] = planeToWorld(tabDefinition.platePlane, linePoints[i]);
        }

        setExternalDisambiguation(context, lineId + unstableIdComponent(i), qUnion(tabGeometry.query, next.query));
        opFitSpline(context, lineId + unstableIdComponent(i), {
                    "points" : linePoints
                });
    }
}

function getTabFaces(context is Context, id is Id, tabDefinition is map, tabGeometryArray is array, sketchGeometryId is Id) returns array
{
    opFitSpline(context, id + "fitSpline", { "points" : [tabDefinition.platePlane.origin + tabDefinition.platePlane.x * tabDefinition.calculatedLength, tabDefinition.platePlane.origin + -tabDefinition.platePlane.x * tabDefinition.calculatedLength] });

    opExtrude(context, id + "extrude", {
                "entities" : qCreatedBy(id + "fitSpline", EntityType.EDGE),
                "direction" : tabDefinition.platePlane->yAxis(),
                "endBound" : BoundingType.BLIND,
                "endDepth" : tabDefinition.calculatedLength,
                "startBound" : BoundingType.BLIND,
                "startDepth" : tabDefinition.calculatedLength
            });
    const largeFace = qCreatedBy(id + "extrude", EntityType.FACE);

    var plateFaceEdges = qNothing();
    if (tabDefinition.booleanToPlate)
    {
        try
        {
            opBoolean(context, id + "cutFacesWithPlate", {
                        "tools" : tabDefinition.plate,
                        "targets" : qCreatedBy(id + "extrude", EntityType.BODY),
                        "keepTools" : true,
                        "makeSolid" : true,
                        "eraseImprintedEdges" : true,
                        "detectAdjacencyForSheets" : false,
                        "recomputeMatches" : true,
                        "operationType" : BooleanOperationType.SUBTRACTION
                    });
        }
        plateFaceEdges = qCoincidesWithPlane(qCapEntity(tabDefinition.extrudeId, CapType.EITHER, EntityType.FACE), tabDefinition.platePlane)->qLoopEdges();
    }

    const sketchEdges = qCreatedBy(sketchGeometryId, EntityType.EDGE);
    opSplitFace(context, id + "splitFace", {
                "faceTargets" : largeFace,
                "edgeTools" : qUnion(sketchEdges, plateFaceEdges),
                "direction" : tabDefinition.platePlane.normal
            });

    const resultFaces = largeFace->qSubtraction(largeFace->qLargest());

    var facesToExtrude = [];
    for (var face in evaluateQuery(context, resultFaces))
    {
        const dependencies = qLoopEdges(face)->qDependency();
        if (!isQueryEmpty(context, qIntersection(dependencies, sketchEdges)))
        {
            facesToExtrude = append(facesToExtrude, face);
        }
    }

    return facesToExtrude;
}

function addTabEnds(context is Context, tabDefinition is map, tabGeometryArray is array) returns array
{
    if (!tabDefinition.enableTabEnds)
    {
        return tabGeometryArray;
    }

    tabDefinition.holeMap = mergeMaps(tabDefinition.holeMap, tabDefinition.existingHoleMap);

    if (!isQueryEmpty(context, tabDefinition.tabStart))
    {
        const tabEnd = getTabEnd(context, tabDefinition, tabDefinition.tabStart);
        tabGeometryArray = concatenateArrays([[tabEnd], tabGeometryArray]);
    }

    if (!isQueryEmpty(context, tabDefinition.tabEnd))
    {
        const tabEnd = getTabEnd(context, tabDefinition, tabDefinition.tabEnd);
        tabGeometryArray = append(tabGeometryArray, tabEnd);
    }

    return addAribitraryPointsToDirections(context, tabDefinition, tabGeometryArray);
}

function getTabEnd(context is Context, tabDefinition is map, tabEndQuery is Query) returns map
{
    if (isPointQuery(context, tabEndQuery))
    {
        const point = evVertexPoint(context, { "vertex" : tabEndQuery })->projectToPlane(tabDefinition.platePlane);
        if (isPlateHole(context, tabDefinition.existingHoleMap, point))
        {
            const outerRadius = getPlateHoleOuterRadius(context, tabDefinition.existingHoleMap, point);
            return { "query" : tabEndQuery, "point" : point, "outerRadius" : outerRadius, "type" : TabGeometryType.HOLE };
        }
        else
        {
            return { "query" : tabEndQuery, "point" : point, "type" : TabGeometryType.POINT };
        }
    }
    else if (!isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.CYLINDER)))
    {
        const point = evSurfaceDefinition(context, { "face" : tabEndQuery }).coordSystem.origin->projectToPlane(tabDefinition.platePlane);
        if (isPlateHole(context, tabDefinition.existingHoleMap, point))
        {
            const outerRadius = getPlateHoleOuterRadius(context, tabDefinition.existingHoleMap, point);
            return { "query" : tabEndQuery, "point" : point, "outerRadius" : outerRadius, "type" : TabGeometryType.HOLE };
        }

        throwEndAwareRegenError(context, tabDefinition, plateError(PlateError.TAB_CYLINDER_INVALID), tabEndQuery);
    }
    else if (isDirectionQuery(context, tabEndQuery))
    {
        if (isQueryEmpty(context, tabDefinition.entitiesToCollideWith))
        {
            throwEndAwareRegenError(context, tabDefinition, plateError(PlateError.TAB_DIRECTION_NO_SCOPE), tabEndQuery);
        }

        const direction = extractDirection(context, tabEndQuery);
        if (!perpendicularVectors(direction, tabDefinition.platePlane.normal))
        {
            throw regenError(plateError(PlateError.TAB_DIRECTION_NOT_PARALLEL), tabEndQuery);
        }

        return { "query" : tabEndQuery, "direction" : direction, "type" : TabGeometryType.DIRECTION };
    }

    throwEndAwareRegenError(context, tabDefinition, plateError(PlateError.TAB_INVALID_END), tabEndQuery);
}

function addAribitraryPointsToDirections(context is Context, tabDefinition is map, tabGeometryArray is array) returns array
{
    for (var i in [0, size(tabGeometryArray) - 1])
    {
        if (tabGeometryArray[i]["type"] == TabGeometryType.DIRECTION)
        {
            tabGeometryArray[i].arbitraryPoint = getClosestRaycast(context, tabDefinition, tabGeometryArray[i], (i == 0 ? getNext(tabGeometryArray, i) : getPrevious(tabGeometryArray, i)).point);
        }
    }
    return tabGeometryArray;
}

/**
 * Tries casting a ray starting from the startPoint along the direction specified by tabGeometry.
 * @returns a 2D point representing the raycast hit.
 */
function getClosestRaycast(context is Context, tabDefinition is map, tabGeometry is map, startPoint is Vector)
precondition
{
    is2dPoint(startPoint);
}
{
    startPoint = planeToWorld(tabDefinition.platePlane, startPoint);
    var result = evRaycast(context, {
            "entities" : tabDefinition.entitiesToCollideWith,
            "ray" : line(startPoint, tabGeometry.direction),
            "closest" : true
        });

    if (result == [])
    {
        result = evRaycast(context, {
                    "entities" : tabDefinition.entitiesToCollideWith,
                    "ray" : line(startPoint, -tabGeometry.direction),
                    "closest" : true
                });
    }

    if (result != [])
    {
        return result[0].intersection->projectToPlane(tabDefinition.platePlane);
    }

    throwNoRaycastHitsError(context, tabDefinition, tabGeometry, line(startPoint, tabGeometry.direction));
}

function throwNoRaycastHitsError(context is Context, tabDefinition is map, tabGeometry is map, raycastLine is Line)
{
    addDebugEdge(context, raycastLine.origin - tabDefinition.calculatedLength * raycastLine.direction, raycastLine.origin + tabDefinition.calculatedLength * raycastLine.direction, DebugColor.RED);
    throwEndAwareRegenError(context, tabDefinition, plateError(PlateError.TAB_NO_RAYCAST_HITS), tabGeometry.query);
}

/**
 * @param errorMsg : @autocomplete `plateError(PlateError.TAB_)`
 */
function throwEndAwareRegenError(context is Context, tabDefinition is map, errorMsg is string, tabEndQuery is Query)
{
    if (areQueriesEquivalent(context, tabDefinition.tabStart, tabEndQuery))
    {
        throw regenError(errorMsg, ["tabStart"], tabEndQuery);
    }
    else if (areQueriesEquivalent(context, tabDefinition.tabEnd, tabEndQuery))
    {
        throw regenError(errorMsg, ["tabEnd"], tabEndQuery);
    }
    else
    {
        throw regenError(errorMsg, tabEndQuery);
    }
}

/**
 * Returns `true` if query evaluates to a vertex or mate connector.
 */
export predicate isPointQuery(context is Context, tabEndQuery is Query)
{
    !isQueryEmpty(context, tabEndQuery->qEntityFilter(EntityType.VERTEX)) || !isQueryEmpty(context, tabEndQuery->qBodyType(BodyType.MATE_CONNECTOR));
}

export predicate isDirectionQuery(context is Context, tabEndQuery is Query)
{
    !isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.PLANE)) ||
        !isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.LINE)) ||
        !isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.CIRCLE)) ||
        !isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.ARC));
    // !isQueryEmpty(context, tabEndQuery->qGeometry(GeometryType.CYLINDER));
}
