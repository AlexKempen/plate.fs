FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "onshape/std/tool.fs", version : "1560.0");

export import(path : "640a885a2e2c48f47fd65ca1", version : "f79cd160c145830f68402e82");

export predicate plateExtrudeFeaturePredicate(definition is map)
{
    booleanStepTypePredicate(definition);

    annotation { "Name" : "Faces to extrude", "Filter" : EntityType.FACE && GeometryType.PLANE && ConstructionObject.NO }
    definition.faces is Query;

    annotation { "Name" : "Faces to remove", "Filter" : EntityType.FACE && GeometryType.PLANE && ConstructionObject.NO,
                "Description" : "Additional faces to remove from the plate." }
    definition.facesToRemove is Query;

    platePositionPredicate(definition);

    if (definition.operationType == NewBodyOperationType.NEW)
    {
        annotation { "Name" : "Extrude seperately", "Description" : "Whether to extrude selections seperately, causing connected faces to be extruded into distinct parts." }
        definition.extrudeSeperately is boolean;
    }

    booleanStepScopePredicate(definition);
}

function verifyPlateExtrudeFeatureDefinition(context is Context, definition is map)
{
    verifyNonemptyQuery(context, definition, "faces", plateError(PlateError.EXTRUDE_SELECT_FACE));

    if (definition.endBound == PlateBoundingType.UP_TO)
    {
        verifyNonemptyQuery(context, definition, "endBoundEntity", plateError(PlateError.EXTRUDE_SELECT_UP_TO_ENTITY));
    }
}

annotation { "Feature Type Name" : "Plate extrude",
        "Filter Selector" : FILTER_SELECTOR,
        "Feature Type Description" : PLATE_EXTRUDE_DESCRIPTION,
        "Manipulator Change Function" : "plateExtrudeManipulatorChange",
        "Editing Logic Function" : "plateExtrudeFeatureEditLogic" }
export const plateExtrude = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        plateExtrudeFeaturePredicate(definition);
    }
    {
        verifyPlateExtrudeFeatureDefinition(context, definition);

        var toDelete = initializeBox();
        const remainingTransform = getRemainderPatternTransform(context, { "references" : qUnion([definition.faces, definition.platePlane]) });

        const result = getPlatePlaneAndDepth(context, definition, "faces");
        const platePlane = result.platePlane;
        const oppositePlane = result.oppositePlane;
        const depth = result.depth;

        var plateFaces;
        if (size(evaluateQuery(context, definition.faces)) >= 2 && !areQueriesEquivalent(context, definition.faces, qParallelPlanes(definition.faces, platePlane, true)))
        {
            throw regenError(plateError(PlateError.EXTRUDE_INPUT_NOT_PARALLEL), ["faces"], definition.faces);
        }

        if (!areQueriesEquivalent(context, definition.faces->qNthElement(0), qParallelPlanes(definition.faces->qNthElement(0), platePlane, true)))
        {
            throw regenError(plateError(PlateError.EXTRUDE_INPUT_NOT_PARALLEL_TO_PLANE), ["faces", "platePlane"], qUnion(definition.faces, definition.platePlane));
        }

        plateFaces = projectFaces(context, id, definition, platePlane, toDelete);


        const centroid = getCentroid(context, definition.faces->qNthElement(0), platePlane);
        addPlateExtrudeManipulator(context, id, definition, centroid, platePlane);

        const extrudeId = id + "plateExtrude";
        
        opExtrude(context, extrudeId, {
                    "entities" : plateFaces,
                    "direction" : platePlane.normal,
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : depth
                });

        const plates = qCreatedBy(extrudeId, EntityType.BODY);

        if (isQueryEmpty(context, qCreatedBy(extrudeId, EntityType.BODY)))
        {
            throw regenError(plateError(PlateError.EXTRUDE_FAILED));
        }

        transformResultIfNecessary(context, id, remainingTransform);

        processNewBodyIfNeeded(context, id, definition, function(id)
            {
                const extrudeFunction = definition.extrudeSeperately ? opExtrudeSeparate : opExtrude;
                extrudeFunction(context, extrudeId, {
                            "entities" : plateFaces,
                            "direction" : platePlane.normal,
                            "endBound" : BoundingType.BLIND,
                            "endDepth" : depth
                        });
                transformResultIfNecessary(context, id, remainingTransform);
            });

        for (var plate in evaluateQuery(context, plates))
        {
            const plateAttribute = plateAttribute(context, {
                        "query" : plate,
                        "platePlane" : platePlane,
                        "oppositePlane" : oppositePlane,
                        "depth" : depth,
                        "centroid" : centroid,
                        "extrudeId" : lastModifyingOperationId(context, plate),
                        "isBooleaned" : definition.operationType != NewBodyOperationType.NEW
                    });
            setPlateAttribute(context, plateAttribute);

            const plateIndex = evaluateQuery(context, qHasAttribute("plate"))->size();
            setProperty(context, {
                        "entities" : plate,
                        "propertyType" : PropertyType.NAME,
                        "value" : "Plate " ~ plateIndex
                    });
        }

        clearBox(context, id, toDelete);
    });

function projectFaces(context is Context, id is Id, definition is map, projectPlane is Plane, toDelete is box) returns Query
{
    var projectedSurfaces = new box([]);
    const projectedFacesId = id + "projectFaces";
    forEachEntity(context, projectedFacesId, definition.faces, function(face is Query, id is Id)
        {
            var offset = 0 * meter;
            if (isQueryEmpty(context, qCoincidesWithPlane(face, projectPlane)))
            {
                const facePlane = evPlane(context, { "face" : face });
                const result = evDistance(context, {
                            "side0" : facePlane,
                            "side1" : projectPlane
                        });
                offset = result.distance * (dot(facePlane.normal, projectPlane.origin - result.sides[0].point) > 0 ? 1 : -1);
            }

            opExtractSurface(context, id + "extractSurface", { "faces" : face, "offset" : offset });
            projectedSurfaces[] = append(projectedSurfaces[], qCreatedBy(id + "extractSurface", EntityType.BODY));
        });

    if (projectedSurfaces[] == [] && !isQueryEmpty(context, definition.platePlane))
    {
        throw platePlaneAwareRegenError(context, definition, plateError(PlateError.EXTRUDE_PROJECT_FAILED), ["faces"], definition.faces);
    }
    else
    {
        toDelete[] = append(toDelete[], qUnion(projectedSurfaces[]));
    }

    if (!definition.extrudeSeperately && size(evaluateQuery(context, qUnion(projectedSurfaces[]))) > 1)
    {
        try(opBoolean(context, id + "projectedSurfaceBoolean", {
                        "tools" : qCreatedBy(projectedFacesId, EntityType.BODY),
                        "operationType" : BooleanOperationType.UNION
                    }));
    }

    if (!isQueryEmpty(context, definition.facesToRemove))
    {
        forEachEntity(context, id + "extrudeRemove", definition.facesToRemove, function(face is Query, id is Id)
            {
                opExtrude(context, id, {
                            "entities" : face,
                            "direction" : projectPlane.normal,
                            "endBound" : BoundingType.THROUGH_ALL,
                            "startBound" : BoundingType.THROUGH_ALL
                        });
            });

        opBoolean(context, id + "booleanRemove", {
                    "tools" : qCreatedBy(id + "extrudeRemove", EntityType.BODY),
                    "targets" : qUnion(projectedSurfaces[]),
                    "operationType" : BooleanOperationType.SUBTRACTION
                });
    }

    return qUnion(projectedSurfaces[])->qOwnedByBody(EntityType.FACE);
}

export function plateExtrudeFeatureEditLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, specifiedParameters is map) returns map
{
    if (definition.endBound == PlateBoundingType.UP_TO &&
        ((oldDefinition.endBoundEntity != definition.endBoundEntity && !isQueryEmpty(context, definition.endBoundEntity)) ||
                (!specifiedParameters.plateOppositeDirection && oldDefinition.platePlane != definition.platePlane)))
    {
        definition.endBoundEntity = definition.endBoundEntity->qNthElement(0);
        var platePlane = getPlatePlaneAndDepth(context, definition, "faces").platePlane;
        if (hasValidEndBoundEntity(context, definition, platePlane))
        {
            var endBoundPlane;
            if (!isQueryEmpty(context, qUnion([definition.endBoundEntity->qEntityFilter(EntityType.VERTEX), definition.endBoundEntity->qBodyType(BodyType.MATE_CONNECTOR)])))
            {
                endBoundPlane = evVertexCoordSystem(context, { "vertex" : definition.endBoundEntity })->plane();
            }
            else if (!isQueryEmpty(context, definition.endBoundEntity->qEntityFilter(EntityType.BODY)->qGeometry(GeometryType.PLANE)))
            {
                endBoundPlane = evPlane(context, { "face" : definition.endBoundEntity });
            }
            else
            {
                return definition;
            }

            const plateToEndBoundVector = project(endBoundPlane, platePlane.origin) - platePlane.origin;
            const result = round(dot(platePlane.normal, plateToEndBoundVector) / norm(plateToEndBoundVector)); // norm(platePlane.normal) = 1
            if (result == -1) // vectors are anti-parallel
            {
                definition.plateOppositeDirection = !definition.plateOppositeDirection;
            }
        }
    }
    return definition;
}
