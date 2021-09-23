FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "onshape/std/fillet.fs", version : "1560.0");


export enum FilletDirection
{
    annotation { "Name" : "Sides" }
    SIDES,
    annotation { "Name" : "Faces" }
    FACES,
    annotation { "Name" : "Both" }
    BOTH
}

export predicate plateFilletFeaturePredicate(definition is map)
{
    annotation { "Name" : "Fillet type", "UIHint" : ["HORIZONTAL_ENUM"] }
    definition.filletType is FilletType;

    if (definition.filletType == FilletType.EDGE)
    {
        annotation { "Name" : "Fillet direction", "UIHint" : ["HORIZONTAL_ENUM"] }
        definition.filletDirection is FilletDirection;

        annotation { "Name" : "Plates to fillet", "Filter" : EntityType.BODY && BodyType.SOLID && ModifiableEntityOnly.YES }
        definition.platesToFillet is Query;

        annotation { "Name" : "Fillet radius" }
        isLength(definition.filletRadius, BLEND_BOUNDS);

        annotation { "Name" : "Faces and edges to exclude", "Filter" : EntityType.FACE || EntityType.EDGE }
        definition.entitiesToExclude is Query;
    }
    else if (definition.filletType == FilletType.FULL_ROUND)
    {
        annotation { "Name" : "Plate side faces to fillet", "Filter" : EntityType.FACE }
        definition.facesToFillet is Query;
    }
}

/**
 * Verifies the definition of plate fillet and returns the plate attributes of selected plates.
 */
function verifyPlateFilletFeatureDefinition(context is Context, definition is map)
{
    verifyContextHasValidPlate(context);

    if (definition.filletType == FilletType.EDGE && isQueryEmpty(context, definition.platesToFillet))
    {
        highlightPlates(context, HighlightType.BODIES);
        verifyNonemptyQuery(context, definition, "platesToFillet", plateError(PlateError.FILLET_SELECT_PLATE));
    }
    else if (definition.filletType == FilletType.FULL_ROUND && isQueryEmpty(context, definition.facesToFillet))
    {
        highlightPlates(context, HighlightType.SIDES);
        verifyNonemptyQuery(context, definition, "facesToFillet", plateError(PlateError.FILLET_SELECT_FACES));

        verifyFilletFacesAreSideFaces(context, definition);
    }
}

function verifyFilletFacesAreSideFaces(context is Context, definition is map)
{

}

annotation { "Feature Type Name" : "Plate fillet",
        "Filter Selector" : FILTER_SELECTOR,
        "Feature Type Description" : PLATE_FILLET_DESCRIPTION }
export const plateFillet = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        plateFilletFeaturePredicate(definition);
    }
    {
        verifyPlateFilletFeatureDefinition(context, definition);

        if (definition.filletType == FilletType.EDGE)
        {
            const plateAttributes = getAndVerifyPlateAttributes(context, definition);

            var failedEdges = [];
            var successes = 0;
            for (var i, plateAttribute in plateAttributes)
            {
                const filletEdges = getFilletEdges(definition, plateAttribute);

                if (isQueryEmpty(context, qUnion(filletEdges)))
                {
                    throw regenError(plateError(PlateError.FILLET_NO_EDGES), plateAttribute.query);
                }

                try
                {
                    setExternalDisambiguation(context, id + unstableIdComponent(i), plateAttribute.query);
                    opFillet(context, id + unstableIdComponent(i) + "fillet", {
                                "entities" : qUnion(filletEdges),
                                "radius" : definition.filletRadius
                            });
                    successes += 1;
                }
                catch
                {
                    failedEdges = append(failedEdges, qUnion(plateAttribute.query, qCreatedBy(id + unstableIdComponent(i) + "fillet", EntityType.EDGE)));
                }
            }

            if (!isQueryEmpty(context, qUnion(failedEdges)))
            {
                if (successes > 0)
                {
                    throw regenError(plateError(PlateError.FILLET_PARTIALLY_FAILED), ["platesToFillet"], qUnion(failedEdges));
                }
                else
                {
                    throw regenError(plateError(PlateError.FILLET_FAILED), ["platesToFillet"], qUnion(failedEdges));
                }
            }
        }
        else if (definition.filletType == FilletType.FULL_ROUND)
        {
            const faceAttributeMap = getAndVerifyRoundPlateAttributes(context, definition);

            var failedFaces = [];
            var i = 0;
            for (var value in faceAttributeMap)
            {
                const face = value.key;
                const plateAttribute = value.value;
                const plateFaces = qNonCapEntity(plateAttribute.extrudeId, EntityType.FACE)->qIntersection(qOwnedByBody(plateAttribute.query, EntityType.FACE));

                const adjacentFaces = qIntersection(plateFaces, qAdjacent(face, AdjacencyType.EDGE, EntityType.FACE));
                if (size(evaluateQuery(context, adjacentFaces)) != 2)
                {
                    failedFaces = append(failedFaces, qUnion(adjacentFaces, face));
                }
                else
                {
                    try
                    {
                        setExternalDisambiguation(context, id + unstableIdComponent(i), face);
                        opFullRoundFillet(context, id + unstableIdComponent(i) + "fullRoundFillet", {
                                    "side1Face" : adjacentFaces->qNthElement(0),
                                    "side2Face" : adjacentFaces->qNthElement(1),
                                    "centerFaces" : face
                                });
                    }
                    catch
                    {
                        failedFaces = append(failedFaces, qUnion(adjacentFaces, face));
                    }
                }
                i += 1;
            }

            if (!isQueryEmpty(context, qUnion(failedFaces)))
            {
                throw regenError(ErrorStringEnum.FILLET_FAILED, ["facesToFillet"], qUnion(failedFaces));
            }
        }
    });

/**
 * Returns an array of PlateAttributes. Throws if definition.platesToFillet has any invalid entities.
 */
function getAndVerifyPlateAttributes(context is Context, definition is map) returns array
{
    if (!isQueryEmpty(context, definition.platesToFillet->qSubtraction(qHasAttribute(definition.platesToFillet, "plate"))))
    {
        const invalidPlates = definition.platesToFillet->qSubtraction(qHasAttribute(definition.platesToFillet, "plate"));
        throw regenError(plateError(PlateError.INVALID_PLATE_SELECTION), ["platesToFillet"], invalidPlates);
    }

    const plateAttributes = getAttributes(context, {
                "entities" : definition.platesToFillet,
                "name" : "plate"
            });

    var invalidPlates = [];
    for (var plateAttribute in plateAttributes)
    {
        if (!(plateAttribute is PlateAttribute))
        {
            invalidPlates = append(invalidPlates, plateAttribute.query);
        }
    }

    if (invalidPlates != [])
    {
        throw regenError(plateError(PlateError.INVALID_PLATE_SELECTION), ["plates"], qUnion(invalidPlates));
    }

    return plateAttributes;
}

/**
 * Creates a map with keys equal to faces and values equal to the attribute of the owner plate of the face.
 */
function getAndVerifyRoundPlateAttributes(context is Context, definition is map) returns map
{
    var faceAttributeMap = {};
    var failedFaces = [];
    for (var face in evaluateQuery(context, definition.facesToFillet))
    {
        const plateAttribute = getAttribute(context, {
                    "entity" : face->qOwnerBody(),
                    "name" : "plate"
                });

        if (!(plateAttribute is PlateAttribute))
        {
            failedFaces = append(failedFaces, face);
        }

        faceAttributeMap[face] = plateAttribute;
    }

    if (!isQueryEmpty(context, qUnion(failedFaces)))
    {
        throw regenError(plateError(PlateError.FILLET_FACE_INVALID_OWNER), ["facesToFillet"], qUnion(failedFaces));
    }

    return faceAttributeMap;
}

predicate needSides(definition is map)
{
    definition.filletDirection == FilletDirection.SIDES || definition.filletDirection == FilletDirection.BOTH;
}

predicate needFaces(definition is map)
{
    definition.filletDirection == FilletDirection.FACES || definition.filletDirection == FilletDirection.BOTH;
}

function getFilletEdges(definition is map, plateAttribute is PlateAttribute) returns array
{
    const edgesToExclude = qUnion(definition.entitiesToExclude->qEntityFilter(EntityType.EDGE), definition.entitiesToExclude->qEntityFilter(EntityType.FACE)->qLoopEdges());

    var edgesToFillet = [];
    if (needSides(definition))
    {
        edgesToFillet = append(edgesToFillet, qParallelEdges(plateEdges(plateAttribute), plateAttribute.platePlane.normal)->qSubtraction(edgesToExclude));
    }

    if (needFaces(definition))
    {
        edgesToFillet = append(edgesToFillet, qCapEntity(plateAttribute.extrudeId, CapType.EITHER, EntityType.FACE)->qLoopEdges()->
                qIntersection(plateEdges(plateAttribute))->
                qSubtraction(edgesToExclude));
    }

    return edgesToFillet;
}

function plateEdges(plateAttribute is PlateAttribute) returns Query
{
    return qOwnedByBody(plateAttribute.query, EntityType.EDGE);
}

