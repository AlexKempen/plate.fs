FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "7d0df6ae3700ad0c09e8c733", version : "1f6e539a98e93003e135f4f2");
export import(path : "d33530b06909083111cf1770/43ca6b9ef034daa4d5f0e097/acea077a563bfb0f1050a27d", version : "12a94c238f322e43917d505b");

/**
 * Translates holes into a set of hole locations on the plate plane.
 * Holes can then be added with [callSubFeatureAndProcessStatus].
 * @param id : @autocomplete `id + "holeLocations"`
 * @param holeDefinition {{
 *      @field userLocations {Query} :
 *              A query for vertices used to locate holes.
 *              @eg `definition.userLocations`
 *      @field oppositeDirection {boolean} :
 *              Whether to flip holes.
 *              @eg `definition.oppositeDirection`
 *      @field platePlane {Plane} :
 *              The hole plane.
 *              @autocomplete `plateAttribute.platePlane`
 *      @field oppositePlane {Plane} :
 *              The hole opposite plane.
 *              @autocomplete `plateAttribute.oppositePlane`
 *      @field depth {ValueWithUnits} :
 *              The depth of the plate.
 *              @autocomplete `plateAttribute.depth`
 *      @field outerRadius {ValueWithUnits} :
 *              The outer radius of created holes.
 *              @autocomplete `getOuterRadius(definition)`
 *      @field holeMap {HoleMap} : @optional
 *              The hole map of existing selected holes.
 *              @autocomplete `plateAttribute.holeMap`
 * }}
 */
export function createHoleLocations(context is Context, id is Id, holeDefinition is map, toDelete is box) returns map
{
    holeDefinition = mergeMaps({ "holeMap" : {} as HoleMap }, holeDefinition);

    holeDefinition.userLocations = clusterVertices(context, {
                "queryToCluster" : holeDefinition.userLocations,
                "clusterType" : ClusterType.PROJECTED_LOCATION,
                "projectPlane" : holeDefinition.platePlane,
                "keepLocations" : true
            });

    const holePlane = holeDefinition.oppositeDirection ? holeDefinition.oppositePlane : holeDefinition.platePlane;

    var newHoleMapBox = new box({});
    var points = new box([]);
    forEachEntity(context, id + "newHoleLocations", holeDefinition.userLocations, function(location is Query, id is Id)
        {
            // cannot projectToPlane here since needs to project to hole plane or plate plane depending on use case
            const point = evVertexPoint(context, { "vertex" : location });
            points[] = append(points[], project(holePlane, point));

            if (isPlateHole(context, holeDefinition.holeMap, point->projectToPlane(holeDefinition.platePlane)))
            {
                reportFeatureInfo(context, id, plateError(PlateError.HOLE_DUPLICATE));
            }
            newHoleMapBox[][getHoleMapKey(point->projectToPlane(holeDefinition.platePlane))] = { "query" : location, "outerRadius" : holeDefinition.outerRadius };

            const locationSketch = newSketchOnPlane(context, id + "locationSketch", { "sketchPlane" : holePlane });
            skPoint(locationSketch, "holePoint", { "position" : point->projectToPlane(holePlane) });
            skSolve(locationSketch);
        });
        
    for (var point in points[])
    {
        addDebugArrow(context, point, point + holePlane.normal * 0.05 * meter, .25 * centimeter, DebugColor.BLUE);
    }
    
    const newDefinition = { "locations" : qCreatedBy(id + "newHoleLocations", EntityType.BODY), "oppositeDirection" : true };

    toDelete[] = append(toDelete[], qCreatedBy(id + "newHoleLocations", EntityType.BODY));
    return { "newDefinition" : newDefinition, "newHoleMap" : newHoleMapBox[] as HoleMap };
}

/**
 * Additional checks for features with plate holes.
 */
export function verifyPlateHoleDefinition(context is Context, definition is map, depth is ValueWithUnits)
{
    if (definition.endStyle == HoleEndStyle.BLIND_IN_LAST)
    {
        verifyNonemptyQuery(context, definition, "scope", plateError(PlateError.HOLE_SELECT_LAST_BODY));
    }

    if (definition.holeStyle == HoleStyle.C_BORE && definition.cBoreDepth >= depth && isQueryEmpty(context, definition.scope))
    {
        throw regenError(plateError(PlateError.HOLE_C_BORE_DEPTH), ["cBoreDepth"]);
    }
}

export function verifyHoleOuterDiameter(definition is map)
{
    if (definition.outerRadiusType == OuterRadiusType.OUTER_DIAMETER)
    {
        if (definition.style == HoleStyle.SIMPLE && definition.holeDiameter > definition.outerDiameter + TOLERANCE.zeroLength * meter)
        {
            throw regenError(plateError(PlateError.HOLE_OUTER_DIAMETER_TO_SMALL), ["holeDiameter", "outerDiameter"]);
        }
        else if (definition.style == HoleStyle.C_BORE && definition.cBoreDiameter > definition.outerDiameter + TOLERANCE.zeroLength * meter)
        {
            throw regenError(plateError(PlateError.HOLE_OUTER_DIAMETER_TO_SMALL), ["cBoreDiameter", "outerDiameter"]);
        }
        else if (definition.style == HoleStyle.C_SINK && definition.cSinkDiameter > definition.outerDiameter + TOLERANCE.zeroLength * meter)
        {
            throw regenError(plateError(PlateError.HOLE_OUTER_DIAMETER_TO_SMALL), ["cSinkDiameter", "outerDiameter"]);
        }
    }
}

export function getOuterRadius(definition is map) returns ValueWithUnits
{
    if (definition.outerRadiusType == OuterRadiusType.WALL_THICKNESS)
    {
        if (definition.style == HoleStyle.SIMPLE)
        {
            return definition.wallThickness + definition.holeDiameter / 2;
        }
        else if (definition.style == HoleStyle.C_BORE)
        {
            return definition.wallThickness + definition.cBoreDiameter / 2;
        }
        else if (definition.style == HoleStyle.C_SINK)
        {
            return definition.wallThickness + definition.cSinkDiameter / 2;
        }
    }
    else if (definition.outerRadiusType == OuterRadiusType.OUTER_DIAMETER)
    {
        return definition.outerDiameter / 2;
    }
}

export function getPlateAttribute(context is Context, id is Id, definition is map) returns PlateAttribute
{
    const plateAttribute = getAttribute(context, {
                "entity" : definition.plate,
                "name" : "plate"
            });

    if (plateAttribute is PlateAttribute)
    {
        verifyPlateLastModifyingId(context, id, plateAttribute);
        verifyPlateIsNotBooleaned(context, id, plateAttribute);
        return plateAttribute;
    }

    throw regenError(plateError(PlateError.INVALID_PLATE_SELECTION), ["plate"], definition.plate);
}

/**
 * An editing logic function for holes which keeps the hole lookup table consistent.
 * Does not interact with scope, oppositeDirection, or locations.
 */
export function holeTableEditLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean) returns map
{
    var definitionCopy = mergeMaps(definition, {
            "scope" : qNothing(),
            "locations" : qNothing(),
            "oppositeDirection" : false
        });

    const specifiedParameters = {
            "scope" : true,
            "locations" : true,
            "oppositeDirection" : true
        };

    definitionCopy = holeEditLogic(context, id, oldDefinition, definitionCopy, isCreating, specifiedParameters, qNothing());

    definitionCopy.scope = undefined;
    definitionCopy.locations = undefined;
    definitionCopy.oppositeDirection = undefined;
    return mergeMaps(definition, definitionCopy);
}

