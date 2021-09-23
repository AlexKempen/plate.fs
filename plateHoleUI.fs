FeatureScript 1560;
import(path : "onshape/std/common.fs", version : "1560.0");

PlateWallThickness::import(path : "b91d63a7f02762d2103a7054", version : "3430559ab29fd9aaf1e8c5e6");

/**
 * Creates the parameter userLocations.
 * definition.userLocations is converted into definition.locations by the function createHoleLocations.
 */
export predicate holeLocationsPredicate(definition is map)
{
    annotation { "Name" : "Points to place holes", "Filter" : QueryFilterCompound.ALLOWS_VERTEX, "UIHint" : ["ALLOW_QUERY_ORDER"] }
    definition.userLocations is Query;
}

export predicate holeOuterRadiusPredicate(definition is map)
{
    annotation { "Name" : "Outer radius type", "UIHint" : ["REMEMBER_PREVIOUS_VALUE"] }
    definition.outerRadiusType is OuterRadiusType;

    if (definition.outerRadiusType == OuterRadiusType.WALL_THICKNESS)
    {
        annotation { "Name" : "Wall thickness", "Icon" : PlateWallThickness::BLOB_DATA, "UIHint" : ["REMEMBER_PREVIOUS_VALUE", "SHOW_EXPRESSION"] }
        isLength(definition.wallThickness, BLEND_BOUNDS);
    }
    else if (definition.outerRadiusType == OuterRadiusType.OUTER_DIAMETER)
    {
        annotation { "Name" : "Outer diameter", "UIHint" : ["REMEMBER_PREVIOUS_VALUE", "SHOW_EXPRESSION"] }
        isLength(definition.outerDiameter, HOLE_OUTER_DIAMETER);
    }
}

export predicate holeScopePredicate(definition is map)
{
    annotation { "Name" : "Hole scope", "Filter" : (EntityType.BODY && BodyType.SOLID && ModifiableEntityOnly.YES),
                "Description" : "Additional entities to send holes through." }
    definition.scope is Query;
}

export enum OuterRadiusType
{
    annotation { "Name" : "Wall thickness" }
    WALL_THICKNESS,
    annotation { "Name" : "Outer diameter" }
    OUTER_DIAMETER
}

export const HOLE_OUTER_DIAMETER =
{
            (meter) : [1e-5, 0.0075, 500],
            (centimeter) : 0.75,
            (millimeter) : 7.5,
            (inch) : 0.375,
            (foot) : 0.03,
            (yard) : 0.01
        } as LengthBoundSpec;
