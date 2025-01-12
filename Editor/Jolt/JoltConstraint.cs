#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Linq;

public enum JoltConstraintType 
{
    Fixed,
	Point,
	Hinge,
	Slider,
	Distance,
	Cone,
	SwingTwist,
	SixDOF,
	Path,
	RackAndPinion,
	Gear,
	Pulley
};

public enum EConstraintSpace {
    Local,
    World
}

public enum EPathRotationConstraintType {
    Free,
	ConstrainAroundTangent,
	ConstrainAroundNormal,
	ConstrainAroundBinormal,
	ConstrainToPath,
	FullyConstrained,
}

public enum ESwingType {
    Cone,
    Pyramid
}

public enum EMotorState {
    Off,
    Velocity,
    Position
}

[System.Serializable]
public class MotorSettings {
    [SerializeField]
    public EMotorState m_MotorState = EMotorState.Off;

    [SerializeField]
    public float m_TargetValue = 0;
    
    [SerializeField]
    public float m_MinForceLimit = -float.MaxValue;
    [SerializeField]
    public float m_MaxForceLimit = float.MaxValue;
    [SerializeField]
    public float m_MinTorqueLimit = -float.MaxValue;
    [SerializeField]
    public float m_MaxTorqueLimit = float.MaxValue;


    public Motor GetData() {
        return new Motor(){
            state = m_MotorState.ToString(),
            targetValue = m_TargetValue,
            minForceLimit = m_MinForceLimit,
            maxForceLimit = m_MaxForceLimit,
            minTorqueLimit = m_MinTorqueLimit,
            maxTorqueLimit = m_MaxTorqueLimit
        };
    }
}

public class JoltConstraint : MonoBehaviour
{  
    [SerializeField]
    public string m_ScriptType = "JoltConstraint";
    [SerializeField]
    public GameObject m_Body1;
    [SerializeField]
    public EConstraintSpace m_Space = EConstraintSpace.Local;
    [SerializeField]
    public JoltConstraintType m_ConstraintType = JoltConstraintType.Fixed;
    [SerializeField]
    public Vector3 m_Point1;
    [SerializeField]
    public bool m_UseSameRotation = true;
    [SerializeField]
    public bool m_UseSamePosition = true;
    [SerializeField]
    public Quaternion m_Rotation1 = new Quaternion(0,0,0,1);
    [SerializeField]
    public Vector3 m_FixedPoint1;

    [SerializeField]
    public Vector3 m_Point2;
    [SerializeField]
    public Quaternion m_Rotation2 = new Quaternion(0,0,0,1);
    [SerializeField]
    public Vector3 m_FixedPoint2;

    [SerializeField]
    public float m_MinDistance = -1;
    [SerializeField]
    public float m_MaxDistance = -1;
    [SerializeField]
    public float m_MinLength = 0;
    [SerializeField]
    public float m_MaxLength = -1;
    [SerializeField]
	public float m_LimitsMin = -Mathf.PI;
    [SerializeField]
	public float m_LimitsMax = Mathf.PI;
    [SerializeField]
	public float m_MaxFrictionTorque;
    [SerializeField]
	public float m_MaxFrictionForce;
    
    [SerializeField]
    public float m_HalfConeAngle;

    [SerializeField]
    public ESwingType m_SwingType;
    [SerializeField]
    public float m_NormalHalfConeAngle;
    [SerializeField]
    public float m_PlaneHalfConeAngle;
    [SerializeField]
    public float m_TwistMinAngle;
    [SerializeField]
    public float m_TwistMaxAngle;


    [SerializeField]
	public float m_Ratio = 1;
    [SerializeField]
	public SplineContainer m_Path;
    [SerializeField]
    public Vector3 m_PathPosition;
    [SerializeField]
    public Quaternion m_PathNormal = Quaternion.LookRotation(new Vector3(0,1,0));
    [SerializeField]
    public Quaternion m_PathRotation = new Quaternion(0,0,0,1);
    [SerializeField]
	public float m_PathFraction = 1;
    [SerializeField]
    public EPathRotationConstraintType m_RotationConstraintType = EPathRotationConstraintType.Free;



    [SerializeField]
    public MotorSettings m_Motor1;
    [SerializeField]
    public MotorSettings m_Motor2;

    float[] ToArray(Vector3 v3) {
        return new float[] { v3[0], v3[1], v3[2] };
    }
    float[] ToV3Array(Quaternion rot, Vector3 vector, Matrix4x4 matrix, bool useSpace = false) {
        Vector3 vec = rot * vector;
        if(m_Space == EConstraintSpace.Local || useSpace) {
            Vector3 zero = matrix.MultiplyPoint(Vector3.zero);
            return ToArray(matrix.MultiplyPoint(vec) - zero);
        }
        return ToArray(vec);
    }

    float[] ToArray(Quaternion v4) {
        return new float[] { v4[0], v4[1], v4[2], v4[3] };
    }

    private void OnDrawGizmos() {
        //Render Collision Shape
        //if (Selection.Contains (gameObject) || (m_Body1 != null && Selection.Contains(m_Body1)))
        if (gameObject != null && m_Body1 != null) {
            renderConstraint();
        }
    }

    public Vector3 GetWorldPoint(Vector3 point, Matrix4x4 matrix) {
        if(m_Space == EConstraintSpace.Local) {
            return matrix.MultiplyPoint(point);
        }
        return point;
    }

    public Vector3 SetWorldPoint(Vector3 point, Matrix4x4 matrix) {
        if(m_Space == EConstraintSpace.Local) {
            return matrix.inverse.MultiplyPoint(point);
        }
        return point;
    }

    private void DrawRay(Vector3 point, Quaternion directionQ, Color color, Matrix4x4 matrix) {
        Gizmos.color = color;
        if(m_Space == EConstraintSpace.Local) {
            Gizmos.matrix = matrix;
        } else {
            Gizmos.matrix = Matrix4x4.identity;
        }
        Vector3 direction = directionQ * Vector3.forward;
        Gizmos.DrawLine(point, point + direction);
    }
    private void ConnectPoints(Vector3 p1, Vector3 p2) {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(p1, p2);
    }

    private void DrawArc(Vector3 point, Quaternion axisQ, Quaternion normalQ, float degrees, float radius, Color color, Matrix4x4 matrix) {
        Gizmos.color = color;
        if(m_Space == EConstraintSpace.Local) {
            Gizmos.matrix = matrix;
        } else {
            Gizmos.matrix = Matrix4x4.identity;
        } 
        using (new Handles.DrawingScope(Gizmos.color, Gizmos.matrix)) {
            Vector3 axis = axisQ * Vector3.forward;
            Vector3 normal = normalQ * Vector3.forward;
            Handles.DrawWireArc(point, axis, normal, degrees, radius);
        }
    }

    public Vector3 GetPointAtFraction(float fraction) {
        if(m_Path == null) {
            return new Vector3();
        }

        float3 position = m_Path.EvaluatePosition(0, fraction);
        return new Vector3(position[0], position[1], position[2]);
    }

    private float[][][] GetPathPoints(float3 body1Pos) {
        if(m_Path == null || m_Path.Splines.Count == 0) {
            return new float[0][][];
        }
        Spline spline = m_Path.Splines[0];
        List<float[][]> response = new List<float[][]>();
        foreach(BezierKnot knot in spline.ToArray()) {
            List<float[]> entry = new List<float[]>
            {
                ToArray(knot.Position - body1Pos),
                ToArray(math.mul(knot.Rotation, knot.TangentIn)),
                ToArray(math.mul(knot.Rotation, knot.TangentOut)),
                ToArray(math.mul(knot.Rotation, math.up()))
            };
            response.Add(entry.ToArray());
        }
        return response.ToArray();
    }

    private void DrawConstraintAxis(string label, Vector3 offset, Quaternion rotation, Vector3 axis, Matrix4x4 matrix) {
        if(m_Space == EConstraintSpace.Local) {
            Gizmos.matrix = matrix;
        } else {
            Gizmos.matrix = Matrix4x4.identity;
        } 
        using (new Handles.DrawingScope(Gizmos.color, Gizmos.matrix)) {
            axis = rotation * axis;
            Handles.Label(offset + axis, label);
            Handles.DrawLine(offset, offset + axis, 2);
        }
    }

    private void DrawConstraintArc(string label, Vector3 offset, float target, Matrix4x4 matrix) {
        if(m_Space == EConstraintSpace.Local) {
            Gizmos.matrix = matrix;
        } else {
            Gizmos.matrix = Matrix4x4.identity;
        } 
        using (new Handles.DrawingScope(Gizmos.color, Gizmos.matrix)) {
            Handles.DrawWireArc(offset, Vector3.left, Vector3.forward, target * 180.0f / 3.1415926f, 1f, 3);
            Vector3 arcPoint = offset + Quaternion.AngleAxis(target * 180.0f / 3.1415926f, Vector3.left) * Vector3.forward;
            Handles.DrawLine(offset, arcPoint);
            Handles.Label(arcPoint, label);
        }
    }



    void renderConstraint() {
        Gizmos.color = Color.white;
        var o = new Vector3(0,0.1f, 0);
        var matrix1 = m_Body1.transform.localToWorldMatrix;
        var matrix2 = transform.localToWorldMatrix;
        switch(m_ConstraintType) {
            case JoltConstraintType.Fixed: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Handles.Label(p1+o, "Fixed Constraint");
                Gizmos.DrawWireSphere(p1, 0.1f);
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
            }
            break;
            case JoltConstraintType.Point: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Handles.Label(p1+o, "Point Constraint");
                Gizmos.DrawWireSphere(p1, 0.1f);
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
            }
            break;
            case JoltConstraintType.Hinge: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Gizmos.DrawWireSphere(p1, 0.1f);
                Handles.Label(p1+o, "Hinge Constraint");
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
                Gizmos.color = Handles.zAxisColor;
                DrawConstraintAxis("Hinge", m_Point1, m_Rotation1, Vector3.forward, matrix1);
                DrawConstraintAxis("", m_Point1, m_Rotation1, Vector3.back, matrix1);
                Gizmos.color = Handles.xAxisColor;
                DrawConstraintAxis("Normal", m_Point1, m_Rotation1, Vector3.left, matrix1);
                Gizmos.color = Color.magenta;
                DrawConstraintArc("Min Angle", m_Point1, m_LimitsMin, matrix1);
                DrawConstraintArc("Max Angle", m_Point1, m_LimitsMax, matrix1);
                if(!m_UseSamePosition || !m_UseSameRotation) {
                    Gizmos.color = Handles.zAxisColor;
                    DrawConstraintAxis("Hinge", m_Point2, m_Rotation2, Vector3.forward, matrix2);
                    DrawConstraintAxis("", m_Point2, m_Rotation2, Vector3.back, matrix2);
                    Gizmos.color = Handles.xAxisColor;
                    DrawConstraintAxis("Normal", m_Point2, m_Rotation2, Vector3.left, matrix2);
                    DrawConstraintArc("Min Angle", m_Point2, m_LimitsMin, matrix2);
                    DrawConstraintArc("Max Angle", m_Point2, m_LimitsMax, matrix2);
                }
            }
            break;
            case JoltConstraintType.Slider: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Gizmos.DrawWireSphere(p1, 0.1f);
                Handles.Label(p1+o, "Slider Constraint");
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
                Gizmos.color = Handles.xAxisColor;
                DrawConstraintAxis("Slider", m_Point1, m_Rotation1, Vector3.left, matrix1);
                DrawConstraintAxis("", m_Point1, m_Rotation1, Vector3.right, matrix1);
                if(!m_UseSamePosition || !m_UseSameRotation) {
                    DrawConstraintAxis("Slider", m_Point2, m_Rotation2, Vector3.left, matrix2);
                    DrawConstraintAxis("", m_Point2, m_Rotation2, Vector3.right, matrix2);
                }

            }
            break;
            case JoltConstraintType.Distance: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Handles.Label(p1+o, "Distance Constraint");
                Gizmos.DrawWireSphere(p1, 0.1f);
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
            }
            break;
            case JoltConstraintType.Cone: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Gizmos.DrawWireSphere(p1, 0.1f);
                Handles.Label(p1+o, "Cone Constraint");
                Gizmos.DrawWireSphere(p2, 0.1f);
                ConnectPoints(p1, p2);
                ConnectPoints(m_Body1.transform.position, p1);
                ConnectPoints(transform.position, p2);
                Gizmos.color = Handles.xAxisColor;
                DrawConstraintAxis("Twist", m_Point1, m_Rotation1, Vector3.left, matrix1);
                DrawConstraintAxis("", m_Point1, m_Rotation1, Vector3.right, matrix1);
                if(!m_UseSamePosition || !m_UseSameRotation) {
                    DrawConstraintAxis("Twist", m_Point2, m_Rotation2, Vector3.left, matrix2);
                    DrawConstraintAxis("", m_Point2, m_Rotation2, Vector3.right, matrix2);
                }
            }
            break;
            case JoltConstraintType.Path: {
                if(m_Path != null) {
                    Matrix4x4 trs = Matrix4x4.TRS(m_PathPosition, m_PathRotation, new Vector3(1f,1f,1f));
                    Vector3 p1 = matrix1.MultiplyPoint(trs.MultiplyPoint(new Vector3(0,0,0)));
                    Handles.Label(p1+o, "Path Constraint");
                    ConnectPoints(m_Body1.transform.position, p1);
                    Gizmos.DrawWireSphere(p1, 0.1f);

                    if(m_Path != null && m_Path.Spline.Count > 0) {
                        Spline spline = m_Path.Splines[0];
                    foreach(BezierKnot knot in spline.ToArray()) {
                        Gizmos.DrawWireSphere(knot.Position, 0.1f);
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(knot.Position, knot.Position + math.mul(knot.Rotation, knot.TangentIn));
                        Gizmos.DrawLine(knot.Position, knot.Position + math.mul(knot.Rotation, knot.TangentOut));
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(knot.Position, knot.Position + math.mul(knot.Rotation, math.up()));
                    }
                    }
                    Gizmos.matrix = m_Body1.transform.localToWorldMatrix * trs;
                    Vector3 body2Point = GetPointAtFraction(m_PathFraction / m_Path.Splines[0].GetLength());
                    Gizmos.matrix = Matrix4x4.identity;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(body2Point, 0.1f);
                    ConnectPoints(transform.position, body2Point);
                }
            }
            break;
            case JoltConstraintType.SwingTwist: {

            }
            break;
            case JoltConstraintType.Pulley: {
                Vector3 p1 = GetWorldPoint(m_Point1, matrix1);
                Vector3 p2 = GetWorldPoint(m_Point2, matrix2);
                Handles.Label(p1+o, "Pulley Constraint");
                Gizmos.DrawWireSphere(p1, 0.1f);
                Gizmos.DrawWireSphere(p2, 0.1f);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(m_FixedPoint1, 0.1f);
                Gizmos.DrawWireSphere(m_FixedPoint2, 0.1f);
                Gizmos.DrawLine(m_FixedPoint1, m_FixedPoint2);

                ConnectPoints(p1, m_FixedPoint1);
                Handles.Label(m_FixedPoint1, "Fixed Point 1");
                ConnectPoints(p2, m_FixedPoint2);
                Handles.Label(m_FixedPoint2, "Fixed Point 2");
            }
            break;
        }
    }

    public JoltConstraintData GetData() {
        var matrix1 = m_Body1.transform.localToWorldMatrix;
        var matrix2 = transform.localToWorldMatrix;
        switch(m_ConstraintType) {
            case JoltConstraintType.Fixed:
            return new FixedConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                axisx1 = ToV3Array(m_Rotation1, Vector3.left, matrix1),
                axisy1 = ToV3Array(m_Rotation1, Vector3.up, matrix1),
                point2 = ToArray(m_Point2),
                axisx2 = ToV3Array(m_Rotation2, Vector3.left, matrix2),
                axisy2 = ToV3Array(m_Rotation2, Vector3.up, matrix2),
            };
            case JoltConstraintType.Point:
            return new PointConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                point2 = ToArray(m_Point2)
            };
            case JoltConstraintType.Hinge:
            return new HingeConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                hingeAxis1 = ToV3Array(m_Rotation1, Vector3.forward, matrix1),
                normalAxis1 = ToV3Array(m_Rotation1, Vector3.left, matrix1),
                point2 = ToArray(m_Point2),
                hingeAxis2 = ToV3Array(m_Rotation2, Vector3.forward, matrix2),
                normalAxis2 = ToV3Array(m_Rotation2, Vector3.left, matrix2),
                limitsMin = m_LimitsMin,
                limitsMax = m_LimitsMax,
                maxFrictionTorque = m_MaxFrictionTorque,
                motor1 = m_Motor1.GetData()
            };
            case JoltConstraintType.Slider:
            return new SliderConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                sliderAxis1 = ToV3Array(m_Rotation1, Vector3.left, matrix1),
                normalAxis1 = ToV3Array(m_Rotation1, Vector3.up, matrix1),
                point2 = ToArray(m_Point2),
                sliderAxis2 = ToV3Array(m_Rotation2, Vector3.left, matrix2),
                normalAxis2 = ToV3Array(m_Rotation2, Vector3.up, matrix2),
                limitsMin = m_LimitsMin,
                limitsMax = m_LimitsMax,
                maxFrictionForce = m_MaxFrictionForce,
                motor1 = m_Motor1.GetData()
            };
            case JoltConstraintType.Distance:
            return new DistanceConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                point2 = ToArray(m_Point2),
                minDistance = m_MinDistance,
                maxDistance = m_MaxDistance,
            };
            case JoltConstraintType.Cone:
            return new ConeConstraint {
                space = m_Space.ToString(),
                point1 = ToArray(m_Point1),
                twistAxis1 = ToV3Array(m_Rotation1, Vector3.left, matrix1),
                point2 = ToArray(m_Point2),
                twistAxis2 = ToV3Array(m_Rotation2, Vector3.left, matrix2),
                halfConeAngle = m_HalfConeAngle
            };
            case JoltConstraintType.Path:
            return new PathConstraint {
                path = GetPathPoints(GetWorldPoint(m_Point1, matrix1)),
                closed = (m_Path != null && m_Path.Splines.Count > 0) ? m_Path.Splines[0].Closed : false,
                pathPosition = ToArray(m_PathPosition),
                pathRotation = ToArray(m_PathRotation),
                pathNormal = ToV3Array(m_PathNormal, Vector3.up, matrix1, true),
                pathFraction = m_PathFraction,
                rotationConstraintType = m_RotationConstraintType.ToString(),
                maxFrictionForce = m_MaxFrictionForce,
                motor1 = m_Motor1.GetData()
            };
            case JoltConstraintType.Pulley:
            return new PulleyConstraint {
                space = m_Space.ToString(),
                bodyPoint1 = ToArray(m_Point1),
                bodyPoint2 = ToArray(m_Point2),
                fixedPoint1 = ToArray(m_FixedPoint1),
                fixedPoint2 = ToArray(m_FixedPoint2),
                ratio = m_Ratio,
                minLength = m_MinLength,
                maxLength = m_MaxLength
            };
        }
        return null;
    }
}

#endif