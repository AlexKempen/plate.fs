FeatureScript 1560;
import(path : "7ac78136a9d4d07b6b848266", version : "86352de14b6dd71895d6a694");

export import(path : "onshape/std/tool.fs", version : "1560.0");

import(path : "faf56c54213a8905041a7b23", version : "c32db668a771e84088b4902a");
export import(path : "1c12653638d0290374eca21b", version : "ec918d7c066a20c5ac838116");
export import(path : "640a885a2e2c48f47fd65ca1", version : "f79cd160c145830f68402e82");

export predicate plateTabFeaturePredicate(definition is map)
{
    booleanStepTypePredicate(definition);


    annotation { "Group Name" : "Selections", "Collapsed By Default" : false }
    {
        holeLocationsPredicate(definition);

        tabEndsPredicate(definition);
    }

    annotation { "Group Name" : "Extrude", "Collapsed By Default" : false }
    {
        platePositionPredicate(definition);
    }

    annotation { "Group Name" : "Hole", "Collapsed By Default" : false }
    {
        holeVariablePredicate(definition);

        holePredicate(definition);

        holeOuterRadiusPredicate(definition);
    }

    booleanStepScopePredicate(definition);
}

export function verifyPlateTabFeatureDefinition(context is Context, definition is map)
{
    verifyNonemptyQuery(context, definition, "userLocations", plateError(PlateError.HOLE_SELECT_LOCATIONS));

    // Deprecated with the removal of directions from tabs
    // if (definition.operationType == NewBodyOperationType.NEW &&
    //     definition.enableTabEnds &&
    //     (isDirectionQuery(context, definition.tabStart) || isDirectionQuery(context, definition.tabEnd)))
    // {
    //     verifyNonemptyQuery(context, definition, "boundingEntities", plateError(PlateError.TAB_SELECT_SCOPE));
    // }
}

annotation { "Feature Type Name" : "Plate tab",
        "Filter Selector" : FILTER_SELECTOR,
        "Feature Type Description" : PLATE_TAB_DESCRIPTION,
        "Manipulator Change Function" : "plateExtrudeManipulatorChange",
        "Editing Logic Function" : "plateTabFeatureEditLogic" }
export const plateTab = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        plateTabFeaturePredicate(definition);
    }
    {
        definition = getHoleVariableDefinitionIfNeccessary(definition);
        verifyPlateTabFeatureDefinition(context, definition);

        var toDelete = initializeBox();

        const remainingTransform = getRemainderPatternTransform(context, { "references" : definition.userLocations });

        const platePlaneAndDepthResult = getPlatePlaneAndDepth(context, definition, "userLocations");
        const platePlane = platePlaneAndDepthResult.platePlane;
        const oppositePlane = platePlaneAndDepthResult.oppositePlane;
        const depth = platePlaneAndDepthResult.depth;

        verifyPlateHoleDefinition(context, definition, depth);

        const holeLocationsResult = createHoleLocations(context, id + "holeLocations", {
                    "userLocations" : definition.userLocations,
                    "oppositeDirection" : definition.oppositeDirection,
                    "platePlane" : platePlane,
                    "oppositePlane" : oppositePlane,
                    "depth" : depth,
                    "outerRadius" : getOuterRadius(definition)
                }, toDelete);

        const newHoleMap = holeLocationsResult.newHoleMap;
        definition = mergeMaps(definition, holeLocationsResult.newDefinition);

        const tabScope = getTabScope(context, definition);
        const centroid = getCentroid(context, definition.locations, platePlane);
        addPlateExtrudeManipulator(context, id, definition, centroid, platePlane);

        const tabId = id + "plateTab";
        createPlateTab(context, tabId, {
                    "platePlane" : platePlane,
                    "depth" : depth,
                    "enableTabEnds" : definition.enableTabEnds,
                    "tabStart" : definition.tabStart,
                    "tabEnd" : definition.tabEnd,
                    "holeMap" : newHoleMap,
                    "entitiesToCollideWith" : tabScope,
                    "booleanToPlate" : false
                }, toDelete);

        definition.scope = qUnion([qCreatedBy(tabId, EntityType.BODY)->qBodyType(BodyType.SOLID), tabScope]);

        transformResultIfNecessary(context, id, remainingTransform);

        callSubfeatureAndProcessStatus(id, hole, context, id + "tabHole", definition);

        processNewBodyIfNeeded(context, id, definition, function(id)
            {
                createPlateTab(context, id + "plateTab", {
                            "platePlane" : platePlane,
                            "depth" : depth,
                            "centroid" : centroid,
                            "enableTabEnds" : definition.enableTabEnds,
                            "tabStart" : definition.tabStart,
                            "tabEnd" : definition.tabEnd,
                            "holeMap" : newHoleMap,
                            "entitiesToCollideWith" : tabScope,
                            "booleanToPlate" : false
                        }, toDelete);
            });

        const plateAttribute = plateAttribute(context, {
                    "query" : qCreatedBy(tabId, EntityType.BODY),
                    "platePlane" : platePlane,
                    "oppositePlane" : oppositePlane,
                    "depth" : depth,
                    "centroid" : centroid,
                    "extrudeId" : tabId,
                    "isBooleaned" : definition.operationType != NewBodyOperationType.NEW
                });
        setPlateAttribute(context, plateAttribute);

        clearBox(context, id, toDelete);
    });

function getTabScope(context is Context, definition is map) returns Query
{
    if (definition.newBodyOperationType != NewBodyOperationType.NEW)
    {
        const scopeEntities = definition.defaultScope ? qAllModifiableSolidBodies() : definition.booleanScope;
        return scopeEntities;
    }

    return qNothing();
}

export function plateTabFeatureEditLogic(context is Context, id is Id, oldDefinition is map, definition is map, isCreating is boolean, specifiedParameters is map) returns map
{
    definition = holeTableEditLogic(context, id, oldDefinition, definition, isCreating);
    return definition;
}
