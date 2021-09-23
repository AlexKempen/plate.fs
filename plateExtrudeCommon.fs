FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "4e2184137cb1382425821c51", version : "8d420d2175ef5e375fae054b");

/**
 * This module contains functions useful for features which create plates.
 */

export function getPlatePlaneAndDepth(context is Context, definition is map, parameterToUse is string) returns map
{
    var platePlane = getPlatePlane(context, definition, parameterToUse);

    var depth;
    if (definition.endBound == PlateBoundingType.BLIND)
    {
        depth = definition.depth;
    }
    else if (definition.endBound == PlateBoundingType.THROUGH_ALL)
    {
        try
        {
            depth = evBox3d(context, {
                            "topology" : qAllModifiableSolidBodies(),
                            "tight" : false
                        })->box3dDiagonalLength();
        }
        catch
        {
            throw regenError(ErrorStringEnum.CANNOT_BE_EMPTY, ["endBoundEntity"]);
        }
    }
    else if (definition.endBound == PlateBoundingType.UP_TO)
    {
        verifyEndBoundEntity(context, definition, platePlane, parameterToUse);
        depth = evDistance(context, {
                        "side0" : platePlane,
                        "side1" : definition.endBoundEntity
                    }).distance;

        if (definition.hasOffset)
        {
            depth += definition.offsetOppositeDirection ? -definition.offsetDistance : definition.offsetDistance;
        }

        depth *= definition.symmetric ? 2 : 1;
    }

    platePlane = adjustPlatePlane(context, definition, depth, platePlane);

    return { "depth" : depth,
            "platePlane" : platePlane,
            "oppositePlane" : getOppositePlane(definition, depth, platePlane) };
}

/**
 * Returns a 2D point representing the approximate center of a plate.
 * @param projectPlane {Plane} : @autocomplete `platePlane`
 */
export function getCentroid(context is Context, topology is Query, projectPlane is Plane) returns Vector
{
    return evBox3d(context, { "topology" : topology, "tight" : false })->box3dCenter()->projectToPlane(projectPlane);
}

export function getPlatePlane(context is Context, definition is map, parameterToUse is string)
{
    if (!isQueryEmpty(context, definition.platePlane))
    {
        return evPlane(context, { "face" : definition.platePlane });
    }
    else
    {
        const parameterQuery = definition[parameterToUse];

        for (var query in evaluateQuery(context, parameterQuery))
        {
            if (!isQueryEmpty(context, query->qSketchFilter(SketchObject.YES)))
            {
                return evOwnerSketchPlane(context, { "entity" : query });
            }
            else if (!isQueryEmpty(context, query->qGeometry(GeometryType.PLANE)))
            {
                return evPlane(context, { "face" : query });
            }
        }

        throw regenError(PlateError.UNABLE_TO_DETERMINE_PLANE, [parameterToUse], definition[parameterToUse]);
    }
}

/**
 * Adjusts the plate plane to its final position.
 */
function adjustPlatePlane(context is Context, definition is map, depth is ValueWithUnits, platePlane is Plane)
{
    platePlane.normal *= definition.plateOppositeDirection ? -1 : 1;

    if (definition.symmetric)
    {
        platePlane.origin -= (platePlane.normal * depth / 2);
    }
    return platePlane;
}

function getOppositePlane(definition is map, depth is ValueWithUnits, platePlane is Plane) returns Plane
{
    platePlane.origin += (platePlane.normal * depth);
    platePlane.normal *= -1;
    return platePlane;
}

/**
 * Returns true if endBoundEntity is planar and parallel to platePlane.
 */
export predicate hasValidEndBoundEntity(context is Context, definition is map, platePlane is Plane)
{
    (!isQueryEmpty(context, definition.endBoundEntity->qEntityFilter(EntityType.FACE)->qGeometry(GeometryType.PLANE)) &&
                !isQueryEmpty(context, qParallelPlanes(definition.endBoundEntity, platePlane, true))) ||
        !isQueryEmpty(context, definition.endBoundEntity->qEntityFilter(EntityType.VERTEX)) ||
        !isQueryEmpty(context, definition.endBoundEntity->qBodyType(BodyType.MATE_CONNECTOR));

    isQueryEmpty(context, qCoincidesWithPlane(definition.endBoundEntity, platePlane));
}

function verifyEndBoundEntity(context is Context, definition is map, platePlane is Plane, parameterToUse is string)
{
    if (definition.endBound == PlateBoundingType.UP_TO)
    {
        definition.endBoundEntity = definition.endBoundEntity->qNthElement(0);
        if (!isQueryEmpty(context, qCoincidesWithPlane(definition.endBoundEntity, platePlane)))
        {
            throw platePlaneAwareRegenError(context, definition, plateError(PlateError.EXTRUDE_UP_TO_COINCIDENT), "endBoundEntity", parameterToUse);
        }
        // entity is a non-parallel plane
        else if (!isQueryEmpty(context, definition.endBoundEntity->qEntityFilter(EntityType.FACE)) && isQueryEmpty(context, qParallelPlanes(definition.endBoundEntity, platePlane, true)))
        {
            throw platePlaneAwareRegenError(context, definition, plateError(PlateError.EXTRUDE_UP_TO_NOT_PARALLEL), "endBoundEntity", parameterToUse);
        }
    }
}

/**
 * Allows errors to highlight the underlying source of a plate plane appropriately. In particular,
 * highlights platePlane if a platePlane is selected. Otherwise, throws normally.
 */
export function platePlaneAwareRegenError(context is Context, definition is map, errorString is string, faultyParameter is string, parameterToUse is string)
{
    if (!isQueryEmpty(context, definition.platePlane))
    {
        throw regenError(errorString, [faultyParameter, "platePlane"], qUnion([definition[faultyParameter], definition.platePlane]));
    }
    else
    {
        throw regenError(errorString, [faultyParameter, parameterToUse], qUnion([definition[faultyParameter], definition[parameterToUse]]));
    }
}

const PLATE_EXTRUDE_MANIPULATOR = "plateExtrudeManipulator";

export function addPlateExtrudeManipulator(context is Context, id is Id, definition is map, centroid is Vector, platePlane is Plane)
precondition
{
    is2dPoint(centroid);
}
{
    if (definition.endBound == PlateBoundingType.BLIND)
    {
        addManipulators(context, id, { (PLATE_EXTRUDE_MANIPULATOR) : linearManipulator({
                            "base" : planeToWorld(platePlane, centroid),
                            "direction" : definition.plateOppositeDirection ? platePlane.normal : -platePlane.normal,
                            "offset" : definition.plateOppositeDirection ? definition.depth : -definition.depth,
                            "primaryParameterId" : "depth"
                        }) });
    }
}

export function plateExtrudeManipulatorChange(context is Context, definition is map, newManipulators is map) returns map
{
    if (newManipulators[PLATE_EXTRUDE_MANIPULATOR] is Manipulator)
    {
        const newDepth = newManipulators[PLATE_EXTRUDE_MANIPULATOR].offset;
        definition.depth = abs(newDepth);
        definition.plateOppositeDirection = newDepth > 0;
    }

    return definition;
}

/**
 * Extrudes one or more edges or faces in a given direction with one or two end conditions.
 * Faces get extruded into unique solid bodies and edges get extruded into unique sheet bodies.
 * Feature modified from the Lighten FS written by Ilya Baran and Morgan Bartlett.
 *
 * @param id : @autocomplete `id + "extrude1"`
 * @param definition {{
 *      @field entities {Query} : Edges and faces to extrude.
 *      @field direction {Vector} : The 3d direction in which to extrude.
 *              @eg `evOwnerSketchPlane(context, {"entity" : entities}).normal` to extrude perpendicular to the owner sketch
 *              @eg `evPlane(context, {"face" : entities}).normal` to extrude perpendicular to the first planar entity
 *      @field endBound {BoundingType} : The type of bound at the end of the extrusion. Cannot be `SYMMETRIC` or `UP_TO_VERTEX`.
 *              @eg `BoundingType.BLIND`
 *      @field endDepth {ValueWithUnits} : @requiredif {`endBound` is `BLIND`.}
 *              How far from the `entities` to extrude.
 *              @eg `1 * inch`
 *      @field endBoundEntity {Query} : @requiredif {`endBound` is `UP_TO_SURFACE` or `UP_TO_BODY`.}
 *              The face or body that provides the bound.
 *      @field endTranslationalOffset {ValueWithUnits} : @optional
 *              The translational offset between the extrude end cap and the end bound entity. The direction vector for
 *              this is the same as `direction`: negative values pull the end cap away from the bound entity when
 *              `endDepth` is positive. `endOffset` will only have an effect if `endBound` is `UP_TO_SURFACE`,
 *              `UP_TO_BODY`, or `UP_TO_NEXT`.
 *      @field startBound {BoundingType} : @optional
 *              The type of start bound. Default is for the extrude to start at `entities`. Cannot be `SYMMETRIC` or `UP_TO_VERTEX`.
 *      @field isStartBoundOpposite : @requiredif {is `UP_TO_SURFACE`, `UP_TO_BODY`, or `UP_TO_NEXT`.}
 *              Whether the `startBound` extends in the opposite direction from the profile as the `endBound`. Defaults
 *              to `true` if not supplied.
 *      @field startDepth {ValueWithUnits} : @requiredif {`startBound` is `BLIND`.}
 *              How far from the `entities` to start the extrude.  The direction vector for this is the negative of `direction`:
 *              positive values make the extrusion longer when `endDepth` is positive.
 *      @field startBoundEntity {Query} : @requiredif {`startBound` is `UP_TO_SURFACE` or `UP_TO_BODY`.}
 *              The face or body that provides the bound.
 *      @field startTranslationalOffset {ValueWithUnits} : @optional
 *              The translational offset between the extrude start cap and the start bound entity. The direction vector for
 *              this is the negative of `direction`: negative values pull the end cap away from the bound entity when
 *              `startDepth` is positive. `startOffset` will only have an effect if `startBound` is `UP_TO_SURFACE`,
 *              `UP_TO_BODY`, or `UP_TO_NEXT`.
 * }}
 */
export const opExtrudeSeparate = function(context is Context, id is Id, definition is map)
    {
        var failedEntities = [];

        for (var i, entity in evaluateQuery(context, definition.entities))
        {
            setExternalDisambiguation(context, id + unstableIdComponent(i), entity);
            try
            {
                opExtrude(context, id + unstableIdComponent(i), mergeMaps(definition, { "entities" : entity }));
            }
            catch
            {
                failedEntities = append(failedEntities, entity);
            }
        }

        if (failedEntities != [])
            throw regenError(ErrorStringEnum.EXTRUDE_FAILED, qUnion(failedEntities));
    };
