FeatureScript 1560;
import(path : "onshape/std/common.fs", version : "1560.0");
import(path : "2077af96cbea61255cb1cb19", version : "ef501ac8c9fb438726c3d8a9");
import(path : "e7dd90a326ef67e679a5c603", version : "51bb943e4a3d50f63501fe07");

/**
 * Valid plates have an attribute set that contains information about the plate.
 * @field query {Query} : The query for the plate.
 * @field platePlane {Plane} : The underlying plane representing the plate. The platePlane normal points in the extruded direction of the plate.
 * @field oppositePlane {Plane} : The plane representing the other side of the plate. The plateOppositePlane normal points in the extruded direction of the plate.
 * @field depth {ValueWithUnits} : The depth of the plate.
 * @field centroid {Vector} : A 2D vector representing the approximate center of the plate.
 * @field extrudeId {Id} : The id of the extrude operation which created the plate. Note that the original creation operation may have created other plates as well.
 * @field lastModifyingPlateFeatureId {Id} : The id of the plate feature which last modified the plate. Used in error reporting.
 * @field isBooleaned {boolean} : Whether the plate has been booleaned by its creating feature. Used in error reporting.
 * @field holeMap {HoleMap} : A map of hole locations on the plate.
 */
export type PlateAttribute typecheck canBePlateAttribute;

export predicate canBePlateAttribute(value)
{
    value is map;
    value.query is Query;

    value.platePlane is Plane;
    value.oppositePlane is Plane;
    value.depth is ValueWithUnits;

    value.centroid is Vector;
    value.extrudeId is Id;
    value.lastModifyingPlateFeatureId is Id;
    value.isBooleaned is boolean;

    value.holeMap is map;
    value.holeMap is HoleMap;
}

export type HoleMap typecheck canBeHoleMap;

export predicate canBeHoleMap(holeMap)
{
    for (var key, value in holeMap)
    {
        is2dPoint(key);
        value is map;
        value.outerRadius is ValueWithUnits;
        value.query is Query;
    }
}

/**
 * Converts a given 2D point to a vector suitable for use as a hole map key (by rounding it off).
 * Neccessary to prevent issues with finite tolerances.
 */
export function getHoleMapKey(point is Vector) returns Vector
precondition
{
    is2dPoint(point);
}
{
    return [roundToPrecision(point[0] / meter, 7) * meter, roundToPrecision(point[1] / meter, 7) * meter] as Vector;
}

export predicate isPlateHole(context is Context, holeMap is HoleMap, point is Vector)
precondition
{
    is2dPoint(point);
}
{
    holeMap[getHoleMapKey(point)] != undefined;
}

export function getPlateHoleOuterRadius(context is Context, holeMap is HoleMap, point is Vector) returns ValueWithUnits
precondition
{
    is2dPoint(point);
}
{
    return holeMap[getHoleMapKey(point)].outerRadius;
}

/**
 * Adds a new hole to the holeMap of a plateAttribute. The locationQuery is accepted as an argument for disambiguation purposes.
 */
export function updatePlateAttributeHoleMap(context is Context, plateAttribute is PlateAttribute, point is Vector, outerRadius is ValueWithUnits, locationQuery is Query) returns PlateAttribute
{
    plateAttribute.holeMap[point->projectToPlane(plateAttribute.platePlane)] = { "outerRadius" : outerRadius, "query" : locationQuery };
    return plateAttribute;
}

/**
 * Returns `true` if a hole created by a 3D point is capable of intersecting the plane. Used in error
 * handling in order to warn the user if their hole selection is invalid, and in the hole feature
 * to perform error handling.
 */
export predicate isPointOnPlate(context is Context, plateAttribute is PlateAttribute, point is Vector)
{
    evRaycast(context, {
                    "entities" : plateAttribute.query,
                    "ray" : line(point, plateAttribute.platePlane.normal),
                    "closest" : false,
                    "includeIntersectionsBehind" : true
                }) != []; // true if evRaycast result is not empty
}

// Attribute utility functions
/**
 * A constructor function for plateAttribute. Designed to be used with [setPlateAttribute].
 *
 * @param definition {{
 *      @field query {Query} :
 *              @autocomplete `plate`,
 *      @field platePlane {Plane} :
 *              @autocomplete `platePlane`
 *      @field oppositePlane {Plane} :
 *              @autocomplete `oppositePlane`
 *      @field depth {ValueWithUnits} :
 *              @autocomplete `depth`
 *      @field centroid {Vector} :
 *              @autocomplete `centroid`
 *      @field extrudeId {Id} :
 *              @autocomplete `extrudeId`
 *      @field isBooleaned {boolean} :
 *              @autocomplete `definition.operationType != NewBodyOperationType.NEW`
 *      @field holeMap {HoleMap} : @optional
 *              @autocomplete `{}`
 * }}
 *
 * @seealso [setPlateAttribute]
 */
export function plateAttribute(context is Context, definition is map) returns PlateAttribute
{
    definition = mergeMaps({ "holeMap" : {} as HoleMap }, definition);
    definition = updateLastModifyingPlateFeatureId(context, definition);
    return definition as PlateAttribute;
}

export function getAllPlateAttributes(context is Context) returns array
{
    return getAttributes(context, {
                "entities" : qHasAttribute("plate"),
                "name" : "plate"
            });
}

export function getAllPlateAttributes(context is Context, hiddenBodies is Query) returns array
{
    return getAttributes(context, {
                "entities" : qHasAttribute("plate")->qSubtraction(hiddenBodies),
                "name" : "plate"
            });
}

/**
 * Throws a unique error if the context does not have any valid plates.
 * Used to catch possibly confusing situations where the user has not
 * created any plates yet.
 */
export function verifyContextHasValidPlate(context is Context)
{
    const plateAttributes = getAllPlateAttributes(context);
    for (var plateAttribute in plateAttributes)
    {
        if (plateAttribute is PlateAttribute)
        {
            return;
        }
    }
    throw regenError(plateError(PlateError.NO_PLATE_IN_CONTEXT));
}

export function verifyPlateIsNotBooleaned(context is Context, id is Id, plateAttribute is PlateAttribute)
{
    if (plateAttribute.isBooleaned)
    {
        reportFeatureInfo(context, id, plateError(PlateError.BOOLEANED_PLATE));
    }
}

export function verifyPlateLastModifyingId(context is Context, id is Id, plateAttribute is PlateAttribute)
{
    if (lastModifyingOperationId(context, plateAttribute.query) != plateAttribute.lastModifyingPlateFeatureId)
    {
        reportFeatureInfo(context, id, plateError(PlateError.INVALID_PLATE_EDIT));
    }
}

/**
 * Updates the last modifying feature id of the plate, then re-sets the attribute.
 */
export function setPlateAttribute(context is Context, plateAttribute is PlateAttribute)
{
    plateAttribute = updateLastModifyingPlateFeatureId(context, plateAttribute);
    setAttribute(context, {
                "entities" : plateAttribute.query,
                "name" : "plate",
                "attribute" : plateAttribute
            });
}

function updateLastModifyingPlateFeatureId(context is Context, plateAttribute is PlateAttribute) returns PlateAttribute
{
    plateAttribute.lastModifyingPlateFeatureId = lastModifyingOperationId(context, plateAttribute.query);
    return plateAttribute;
}

export enum HighlightType
{
    BODIES,
    FACES,
    SIDES
}

/**
 * A utility function used to highlight plate geometry. Since debug has a passive performance impact,
 * highlightPlates is typically only for use alongside fatal errors.
 * @param highlightType : @autocomplete `HighlightType.BODIES`
 */
export function highlightPlates(context is Context, highlightType is HighlightType)
{
    var entitiesToHighlight = [];
    if (highlightType == HighlightType.BODIES)
    {
        entitiesToHighlight = [qHasAttribute("plate")];
    }
    else
    {
        const plateAttributes = getAttributes(context, {
                    "entities" : qHasAttribute("plate"),
                    "name" : "plate"
                });
        for (var plateAttribute in plateAttributes)
        {
            if (highlightType == HighlightType.FACES)
            {
                entitiesToHighlight = append(entitiesToHighlight, qCapEntity(plateAttribute.extrudeId, CapType.EITHER, EntityType.FACE));
            }
            else if (highlightType == HighlightType.SIDES)
            {
                entitiesToHighlight = append(entitiesToHighlight, qNonCapEntity(plateAttribute.extrudeId, EntityType.FACE));
            }
        }
    }
    debug(context, qUnion(entitiesToHighlight), DebugColor.BLUE);
}
