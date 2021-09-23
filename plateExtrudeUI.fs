FeatureScript 1560;
import(path : "onshape/std/common.fs", version : "1560.0");

export enum PlateBoundingType
{
    annotation { "Name" : "Blind" }
    BLIND,
    annotation { "Name" : "Up to" }
    UP_TO,
    annotation { "Name" : "Through all" }
    THROUGH_ALL
}

export predicate platePositionPredicate(definition is map)
{
    annotation { "Name" : "Plate plane", "Filter" : QueryFilterCompound.ALLOWS_PLANE, "MaxNumberOfPicks" : 1,
                "Description" : "A flat face or plane to locate the plate on." }
    definition.platePlane is Query;

    annotation { "Name" : "End type" }
    definition.endBound is PlateBoundingType;

    annotation { "Name" : "Opposite direction", "UIHint" : ["OPPOSITE_DIRECTION"] }
    definition.plateOppositeDirection is boolean;

    plateThicknessPredicate(definition);

    annotation { "Name" : "Symmetric" }
    definition.symmetric is boolean;
}

export predicate plateThicknessPredicate(definition is map)
{
    if (definition.endBound == PlateBoundingType.BLIND || definition.endBound == PlateBoundingType.THROUGH_ALL)
    {
        annotation { "Name" : "Depth", "UIHint" : ["REMEMBER_PREVIOUS_VALUE", "SHOW_EXPRESSION"] }
        isLength(definition.depth, LENGTH_BOUNDS);
    }
    else if (definition.endBound == PlateBoundingType.UP_TO)
    {
        annotation { "Name" : "Up to entity",
                    "Filter" : (EntityType.FACE && GeometryType.PLANE && SketchObject.NO) || QueryFilterCompound.ALLOWS_VERTEX,
                    "MaxNumberOfPicks" : 1 }
        definition.endBoundEntity is Query;

        annotation { "Name" : "Offset distance", "Column Name" : "Has offset", "UIHint" : ["DISPLAY_SHORT", "FIRST_IN_ROW"] }
        definition.hasOffset is boolean;

        if (definition.hasOffset)
        {
            annotation { "Name" : "Offset distance", "UIHint" : ["DISPLAY_SHORT"] }
            isLength(definition.offsetDistance, LENGTH_BOUNDS);

            annotation { "Name" : "Opposite direction", "Column Name" : "Offset opposite direction", "UIHint" : ["OPPOSITE_DIRECTION"] }
            definition.offsetOppositeDirection is boolean;
        }
    }
}
