FeatureScript 1560;
/**
 * A collection of functions used to define error messages in plate features.
 */

export const FILTER_SELECTOR = ["Alex", "Alex's FeatureScripts", "Plate"];

export const PLATE_EXTRUDE_DESCRIPTION = "Extrude geometry into flat plates. Also supports a variety of options to optimize the extruding process.";

export const PLATE_TAB_DESCRIPTION = "Create plates built around holes. Serves as an advanced alternative to plate extrude.";

export const PLATE_HOLE_DESCRIPTION = "Quickly add holes to plates created by other plate features. Can also add holes to plates in a complex way.";

export const PLATE_FILLET_DESCRIPTION = "Quickly fillet plates created by other plate features.";

enum StatusType
{
    OK,
    ERROR,
    WARNING,
    INFO
}

/**
 * @internal
 */
const PLATE_ERROR_MAP = {
        // common errors
        PlateError.NO_PLATE_IN_CONTEXT : "Failed to find any valid plate features in the current context. This feature can only operate on plates created with another plate feature.",
        PlateError.SELECT_PLATE : "Select a plate created by a plate feature.",
        PlateError.INVALID_PLATE_SELECTION : "One or more selected entities are not plates created by another plate feature.",
        PlateError.BOOLEANED_PLATE : "The selected plate has been booleaned. Output may be unexpected.",
        PlateError.INVALID_PLATE_EDIT : "The selected plate has been edited by a non-plate feature. Output may be unexpected.",
        PlateError.UNABLE_TO_DETERMINE_PLANE : "Unable to extract a plate plane from selections. Try selecting a plate plane.",

        PlateError.ENTER_HOLE_VARIABLE : "Enter the value of a hole variable from a hole variable feature by calling it with #.",
        PlateError.INVALID_HOLE_VARIABLE : "The entered hole variable is invalid. Ensure it is set up as a hole variable.",

        // plate extrude feature
        PlateError.EXTRUDE_SELECT_FACE : "Select a face or sketch region to extrude into a plate.",
        PlateError.EXTRUDE_SELECT_UP_TO_ENTITY : "Select an entity to terminate the plate.",
        PlateError.EXTRUDE_INPUT_NOT_PARALLEL : "Selected faces are not parallel to each other.",
        PlateError.EXTRUDE_INPUT_NOT_PARALLEL_TO_PLANE : "The selected plate plane is not parallel to the selected faces.",
        PlateError.EXTRUDE_UP_TO_COINCIDENT : "The up to entity cannot be coincident with the plate plane.",
        PlateError.EXTRUDE_UP_TO_NOT_PARALLEL : "The up to entity must be parallel to the plate plane.",
        PlateError.EXTRUDE_PROJECT_FAILED : "Failed to project one or more faces onto the plate plane. Check input.",
        PlateError.EXTRUDE_FAILED : "Failed to extrude plates. Check input.",

        // plate tab feature
        PlateError.TAB_SELECT_SCOPE : "Select entities to terminate hole geometry with.",
        PlateError.TAB_INVALID_END : "Invalid tab end geometry selected.",
        PlateError.TAB_DIRECTION_NO_SCOPE : "A tab end direction has no entities to intersect.",
        PlateError.TAB_DIRECTION_NOT_PARALLEL : "Selected tab end direction is not parallel to the plate plane.",
        PlateError.TAB_DIRECTIONS_INTERSECT : "Selected directions are self intersecting. Check input.",
        PlateError.TAB_NO_RAYCAST_HITS : "Selected direction does not intersect with bounding entities.",
        PlateError.TAB_CYLINDER_INVALID : "Selected cylinder is not part of a hole created by a plate hole feature.",
        PlateError.TAB_GEOMETRY_FAILED : "Failed to compute a valid line. An end point may be too close to a hole.",
        PlateError.TAB_EXTRUDE_FAILED : "Failed to extrude tab. Check input.",
        PlateError.TAB_BOOLEAN_FAILED : "Failed to boolean tab. Check input.",

        // plate hole feature
        PlateError.HOLE_SELECT_SCOPE : "Select a plate and additional entities to merge holes with.",
        PlateError.HOLE_SELECT_LOCATIONS : "Select points to locate holes with.",
        PlateError.HOLE_SELECT_LAST_BODY : "Blind in last requires full intersections with at least two parts. Select an additional entity to merge holes with.",
        PlateError.HOLE_C_BORE_DEPTH : "The counterbore depth exceeds the plate thickness.",
        PlateError.HOLE_OUTER_DIAMETER_TO_SMALL : "The hole outer diameter is smaller than the hole itself.",
        PlateError.HOLE_DUPLICATE : "Selections conflict with existing plate holes. Output may be unexpected.",
        PlateError.HOLE_FAILED : "Failed to create holes. Check input.",

        // plate fillet feature
        PlateError.FILLET_SELECT_PLATE : "Select plates to fillet.",
        PlateError.FILLET_SELECT_FACES : "Select plate side faces to fillet.",
        PlateError.FILLET_FACE_INVALID_OWNER : "One or more selections do not belong to plates created by another plate feature.",
        PlateError.FILLET_NO_EDGES : "Failed to find valid edges to fillet. Check input.",
        PlateError.FILLET_NO_ADJACENT_FACES : "Failed to find two valid faces adjacent to the selected faces.",
        PlateError.FILLET_PARTIALLY_FAILED : "Failed to fillet some selections. Check input.",
        PlateError.FILLET_FAILED : "Failed to fillet selections. Check input."
    };

export enum PlateError
{
    NO_PLATE_IN_CONTEXT,
    SELECT_PLATE,
    INVALID_PLATE_SELECTION,
    BOOLEANED_PLATE,
    INVALID_PLATE_EDIT,
    UNABLE_TO_DETERMINE_PLANE,

    ENTER_HOLE_VARIABLE,
    INVALID_HOLE_VARIABLE,

    EXTRUDE_SELECT_FACE,
    EXTRUDE_SELECT_UP_TO_ENTITY,
    EXTRUDE_INPUT_NOT_PARALLEL,
    EXTRUDE_INPUT_NOT_PARALLEL_TO_PLANE,
    EXTRUDE_UP_TO_COINCIDENT,
    EXTRUDE_UP_TO_NOT_PARALLEL,
    EXTRUDE_PROJECT_FAILED,
    EXTRUDE_FAILED,

    TAB_SELECT_SCOPE,
    TAB_INVALID_END,
    TAB_DIRECTION_NO_SCOPE,
    TAB_DIRECTION_NOT_PARALLEL,
    TAB_DIRECTIONS_INTERSECT,
    TAB_NO_RAYCAST_HITS,
    TAB_CYLINDER_INVALID,
    TAB_GEOMETRY_FAILED,
    TAB_EXTRUDE_FAILED,
    TAB_BOOLEAN_FAILED,

    HOLE_SELECT_SCOPE,
    HOLE_SELECT_LOCATIONS,
    HOLE_SELECT_LAST_BODY,
    HOLE_C_BORE_DEPTH,
    HOLE_OUTER_DIAMETER_TO_SMALL,
    HOLE_DUPLICATE,
    HOLE_FAILED,

    FILLET_SELECT_PLATE,
    FILLET_SELECT_FACES,
    FILLET_FACE_INVALID_OWNER,
    FILLET_NO_EDGES,
    FILLET_NO_ADJACENT_FACES,
    FILLET_PARTIALLY_FAILED,
    FILLET_FAILED
}

/**
 * @param plateError : @autocomplete `PlateError.`
 */
export function plateError(plateError is PlateError) returns string
{
    return PLATE_ERROR_MAP[plateError];
}
