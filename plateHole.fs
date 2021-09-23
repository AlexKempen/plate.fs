FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "1c12653638d0290374eca21b", version : "ec918d7c066a20c5ac838116");
import(path : "faf56c54213a8905041a7b23", version : "98ece2305922d2a6c04ae7ad");

export enum HoleType
{
    annotation { "Name" : "Internal" }
    INTERNAL,
    annotation { "Name" : "Tab" }
    TAB
}

export predicate plateHoleFeaturePredicate(definition is map)
{
    annotation { "Name" : "Plate hole type", "UIHint" : ["HORIZONTAL_ENUM"] }
    definition.holeType is HoleType;

    holeVariablePredicate(definition);

    holePredicate(definition);

    holeLocationsPredicate(definition);

    if (definition.holeType == HoleType.TAB)
    {
        holeOuterRadiusPredicate(definition);

        tabEndsPredicate(definition);
    }

    annotation { "Name" : "Plate", "Filter" : (EntityType.BODY && BodyType.SOLID && ModifiableEntityOnly.YES), "MaxNumberOfPicks" : 1 }
    definition.plate is Query;

    holeScopePredicate(definition);
}

export function verifyPlateHoleFeatureDefinition(context is Context, definition is map)
{
    verifyContextHasValidPlate(context);
    
    if (isQueryEmpty(context, definition.userLocations))
    {
        highlightPlates(context, HighlightType.FACES);
        verifyNonemptyQuery(context, definition, "userLocations", plateError(PlateError.HOLE_SELECT_LOCATIONS));
    }

    if (isQueryEmpty(context, definition.plate))
    {
        highlightPlates(context, HighlightType.BODIES);
        verifyNonemptyQuery(context, definition, "plate", plateError(PlateError.SELECT_PLATE));
    }

    if (definition.holeType == HoleType.TAB)
    {
        verifyHoleOuterDiameter(definition);
    }
}

annotation { "Feature Type Name" : "Plate hole",
        "Filter Selector" : FILTER_SELECTOR,
        "Feature Type Description" : PLATE_HOLE_DESCRIPTION,
        "Editing Logic Function" : "plateHoleFeatureEditLogic" }
export const plateHole = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        plateHoleFeaturePredicate(definition);
    }
    {
        definition = getHoleVariableDefinitionIfNeccessary(definition);
        verifyPlateHoleFeatureDefinition(context, definition);
        var plateAttribute = getPlateAttribute(context, id, definition);
        verifyPlateHoleDefinition(context, definition, plateAttribute.depth);

        var toDelete = initializeBox();
        const remainderTransform = getRemainderPatternTransform(context, { "references" : qUnion([definition.userLocations, definition.tabStart, definition.tabEnd]) });

        const outerRadius = getOuterRadius(definition);

        const result = createHoleLocations(context, id + "holeLocations", {
                    "userLocations" : definition.userLocations,
                    "oppositeDirection" : definition.oppositeDirection,
                    "platePlane" : plateAttribute.platePlane,
                    "oppositePlane" : plateAttribute.oppositePlane,
                    "depth" : plateAttribute.depth,
                    "outerRadius" : outerRadius,
                    "holeMap" : plateAttribute.holeMap
                }, toDelete);

        const newHoleMap = result.newHoleMap;
        definition = mergeMaps(definition, result.newDefinition);

        if (definition.holeType == HoleType.TAB)
        {
            createPlateTab(context, id + "holeTab", {
                        "platePlane" : plateAttribute.platePlane,
                        "depth" : plateAttribute.depth,
                        "enableTabEnds" : definition.enableTabEnds,
                        "tabStart" : definition.tabStart,
                        "tabEnd" : definition.tabEnd,
                        "holeMap" : newHoleMap,
                        "entitiesToCollideWith" : definition.scope,
                        "booleanToPlate" : true,
                        "plate" : plateAttribute.query,
                        "extrudeId" : plateAttribute.extrudeId,
                        "centroid" : plateAttribute.centroid,
                        "existingHoleMap" : plateAttribute.holeMap
                    }, toDelete);
        }

        definition.scope = qUnion(plateAttribute.query, definition.scope);
        transformResultIfNecessary(context, id, remainderTransform);
        callSubfeatureAndProcessStatus(id, hole, context, id + "plateHole", definition);

        if (isQueryEmpty(context, qCreatedBy(id + "plateHole", EntityType.FACE)))
        {
            throw regenError(plateError(PlateError.HOLE_FAILED));
        }

        plateAttribute.holeMap = mergeMaps(plateAttribute.holeMap, newHoleMap); // override existing holes, if present
        setPlateAttribute(context, plateAttribute);

        clearBox(context, id, toDelete);
    });

export function plateHoleFeatureEditLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, hiddenBodies is Query) returns map
{
    definition = holeVariableOverride(context, oldDefinition, definition);
    definition = plateHoleScope(context, id, oldDefinition, definition, hiddenBodies);
    definition = holeTableEditLogic(context, id, oldDefinition, definition, isCreating);

    return definition;
}

function plateHoleScope(context is Context, id is Id, oldDefinition is map, definition is map, hiddenBodies is Query) returns map
{
    if (oldDefinition.holeLocations != definition.holeLocations &&
        size(evaluateQuery(context, definition.holeLocations)) == 1 &&
        isQueryEmpty(context, definition.plate))
    {
        const plateAttributes = getAllPlateAttributes(context, hiddenBodies);
        const point = evVertexPoint(context, { "vertex" : definition.holeLocations });

        var hit;
        for (var plateAttribute in plateAttributes)
        {
            if (isPointOnPlate(context, plateAttribute, point))
            {
                if (hit == undefined)
                {
                    hit = plateAttribute.query;
                }
                else
                {
                    return definition;
                }
            }
        }

        definition.plate = hit;
    }

    return definition;
}


