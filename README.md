
This package is composed of two parts:

## Jolt Physics Markup

This project exists to make markup and annotation of 3D scenes easier for usage with JoltPhysics (https://github.com/jrouwe/JoltPhysics.js).

This is done attaching Jolt Rigid Body components to objects and configuring their collision type.

You will have the option of choosing the appropriate Collision Shape.

For complex (but still compact) shapes, you may group multiple simpler shapes under one Static Compound shape.

It is up to the end application importing the script to properly process this nested behavior.

The markup supports simple body attributes, such as Mass, Friction, and Restitution, as well as indicating the desired motion-types of Jolt Physics: Static, Dynamic, and Kinematic.

Jolt Constraints may be added to Jolt Ridgid Bodies. This supports most of the Constraints that JoltPhysics.js supports.

Some engines may refer to these Constraints as Joints, but this term does not apply to some of the more complex Constraints.

Most of these constraints will default to using a single Constraint point between the two bodies, such as "the point of a hinge between two boards", but this may be overridden.

The constraints will also default to a single constraint access (such as the direction of a hinge), but this may also be overridden. If you do not have a specific reason to override these "use same position, use same rotation", and changing these settings may result in unanticipated.

The graphical markup for constraints allows for selecting position and rotation axis, and seeing a visible Unity Handle that may be dragged around to edit position and rotation settings.

Changing between Constraint Types currently does not clear previous values from the editor, so you may need to modify them. One of the safer options is to change to 'local-space' and set the Point1 and Point2 to 0,0,0 to reset the constraint's points back to the origins of the two bodies.

Many constraints have axis. They should be labeled on the Scene editor, but correspond to:

1. Hinge
* The hinge axis, what axis will it rotate along
* The normal axis. This axis points off the hinge axis to indicate where 0-degrees is. This axis is used to restrict the min/max rotations on a hinge and to set a target destination when using 'Motor: Drive to Target' in radians

2. Slider
* The only axis of significance is the Slider axis. This direction, starting from Point1 or Point2, is the only line that the Body1 or 2 may move along.

3. Cone:
* The only axis of significance is the Twist axis. This axis, starting from Point1 or Point2, defines the tip of a cone limiting the axis of tilt when combined with the halfConeAngle. 
* The triangle created using the combination of the halfConeAngle from the Body1/2, when spun around the twist axis, produces the conceptual Cone.

4. Swing Twist
* The Twist axis defines an axis similar to Cone. Orthogonal to this axis is defined a PlaneAxis, and then there exists a Normal axis.
* The normal axis defines angle 0 for the twist restrictions. THe Twist axis may rotate between Min and Max twist angles from this angle 0
* The planar axis allows for rotation of the twist axis in this direction like a cone, with Twist axis acting as the angle zero, between + and - Plane Half Cone Angle
* The normal axis allows for rotation of the twist axis in this direction like a cone, with Twist axis acting like the angle zero, between + and - Normal Half Cone Angle
* I conceptually view this as a key being put into a lock. The twisting of the key is restricted by the Min/Max angles. You may tilt the key upward and down by Plane Half Cone, and tilt it left/right by Normal Half Cone. You may technically wiggle it up/right/down/left, with the vertical and horizontal angles being restricted into an ellipsis by those two half cones.

The most complicated constraints (configuration wise, with regard to not just defining an axis or two) are:

1. Pulley Constraint. This will require you to specify 2 world-space "Fixed Points" that you can imagine as the hanging hooks that will suspend the two bodies. 
* The Point1 and Point2 values will reference the attachment points on the bodies, so shifting these to non-center positions will cause the bodies to hang off-center.

2. Path Constraints. This will require Body1 is a path-owner. The path will exist, centered around Body1.
  * You may translate the path relative to Body1, and then tilt it at this new location, using the Path Position and Rotation.
  * On-start, Jolt will dynamically generate a Point2 on the path using "Path Fraction" and treat this as a usual (body1-space) Body2 constraint point (indicated on the GUI overlay
  - TODO: modify to use PathPosition instead).
  - TODO: update to support the properties on corner-curving, Catmull-Rom, Hermite

4. Gears - Both bodies must have a hinge constraint. TODO: not implemented, likely will just copy hinge axis from those constraints into this new constraint
5. Gear and Pinion - Body 1 must have a Hinge (gear-like), and Body 2 must be a slider

Support is listed, but currently not implemented, for:
* 6 Degrees of Freedom
* Gears
* Rack and Pinion
  
Note: Constraints are designed right now to be added to Body2, which will then reference Body1.

For several situations, this order does not matter, but in the following it may impact the variables used:

1. Path Constraints - Ratio: The rate of pulling one side and lifting the other (such as a compound pulley) is determined by the ratio. A ratio of 2 means that pulling down Body2 is 2x as effective at lifting Body 1 (Per Jolt Physics: 
  *  Length1 = |BodyPoint1 - FixedPoint1|
  * Length2 = |BodyPoint2 - FixedPoint2|,
  * MinDistance <= Length1 + Ratio * Length2 <= MaxDistance)

3. Path Constraint - Body1 controls the location and offset of the path. Body2 follows the path.
4. Gears - Ratio is number of teeth of Gear2 / Gear1
5. Rack and Pinion - Ratio is [mRatio = 2 * PI * inNumTeethRack / (RackLength * NumTeethPinion)], how much does the Pinion move per 1x rotation of Rack

## Jolt Physics Export

The export script uses heavy usage and modification of the gltFast projects code (https://github.com/Unity-Technologies/com.unity.cloud.gltfast)

To use this script, go to File > Scene > Export Jolt GLTB (currently only supports the binary GLTF 2.0 format)

This script will run the original gltFast GLB export, and then post-process the GLTF 2.0 binary to add GLTF extra attributes to the scene. 

Note: This is currently done by traversing the Unity Scene in the exact same order and logic used in gltFast export to map the GLTF nodes to the Unity scene.
**This means that updates to gltFast may break this 1-to-1 mapping.**

GLTF extra data will be updated with a 'jolt' property, and a corresponding 'collision' property and 'constraint' array. Minor validation may be run to ensure that constraints only exist and point to other Jolt Rigid Bodies, but this is not guaranteed and little-to-no error handling is done right now.

Most bodies will be produced only using a text-based 'shape' and the world-space data (scale, rotation, position) along with extent data. It appears that multiplying extents by scale approximates the actual shape in the Unity editor.
**Note: Skinned Meshes and piece-wise meshes may not have proper extents**

The GLTF file automatically (via the gltFast library) will automatically include the Meshes of a node, which should correspond to the same GLTF node that receives the Jolt collision/constraint extra. This should provide the needed data for deriving any vertex-heavy collision shape (Mesh and ConvexHull)

As a special case, Heightfields in Unity do not use Mesh, so special code was added to compile the Heightfield into a GLTF data format. (TODO: extra code to ensure that Terrain Layer data is shared between all terrain since it is re-used in unity between all terrain)

Heightfields (Terrain in Unity) are stored using a depth-map (referenced in the GLTF data object as an image Index using GLTF standard format of image-storage) in RGB PNG format, encoding the floating-point value between [0x000000 and 0xFFFFFF], scaled  between min-height and max-height. The actual min/max heights will be stored in the JSON to use in reconstruction.
* The terrain texture is stored using one or more Splat-Maps from the TerrainData.
	* See: https://en.wikipedia.org/wiki/Texture_splatting + https://doc.babylonjs.com/toolsAndResources/assetLibraries/materialsLibrary/terrainMat
	* A new splat-map will be made for every 3 terrain layers.
	* This splat-map is stored in JPG format. 
* TODO: Mesh Hole Texture
*  Terrain layers will be stored in an array.
	* Each layer corresponds to one of the color channels, in-order, of a splat map (R,G,B)
	* Each layer will have a tileSize value, corresponding to the world-space size of that texture as an individual square.  You may scale the texture coordinates during render by (terrainWidth / tileSize)
	* Each layer will also have an offset.
	* TODO: BumpMap + texture/scales 