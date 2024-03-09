#if UNITY_EDITOR

    public class Motor {
        public string state;
        public float targetValue;
        public float minForceLimit;
        public float maxForceLimit;
        public float minTorqueLimit;
        public float maxTorqueLimit;
    }
    public class Spring {
        public string mode;
        public float frequency;
        public float stiffness;
        public float damping;
    }

    public class JoltConstraintData {
        public string type {get; set;}
        public int body1 { get; set;}
    }

    public class FixedConstraint: JoltConstraintData{
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] axisx1 { get; set;}
        public float[] axisy1 { get; set;}
        public float[] point2 { get; set;}
        public float[] axisx2 { get; set;}
        public float[] axisy2 { get; set;}
    }

    public class PointConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] point2 { get; set;}
    }
    public class HingeConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] hingeAxis1 { get; set;}
        public float[] normalAxis1 { get; set;}
        public float[] point2 { get; set;}
        public float[] hingeAxis2 { get; set;}
        public float[] normalAxis2 { get; set;}
        public float limitsMin {get; set;}
        public float limitsMax {get; set;}
        public float maxFrictionTorque {get; set;}
        public Spring spring {get; set;}
        public Motor motor1 { get; set;}
    }
    public class SliderConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] sliderAxis1 { get; set;}
        public float[] normalAxis1 { get; set;}
        public float[] point2 { get; set;}
        public float[] sliderAxis2 { get; set;}
        public float[] normalAxis2 { get; set;}
        public float limitsMin {get; set;}
        public float limitsMax {get; set;}
        public float maxFrictionForce {get; set;}
        public Spring spring {get; set;}
        public Motor motor1 { get; set;}
    }
    public class DistanceConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] point2 { get; set;}
        public float minDistance {get; set;}
        public float maxDistance {get; set;}
        public Spring spring {get; set;}
    }
    public class ConeConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] point1 { get; set;}
        public float[] twistAxis1 { get; set;}
        public float[] point2 { get; set;}
        public float[] twistAxis2 { get; set;}
        public float halfConeAngle {get; set;}
    }
    public class PathConstraint: JoltConstraintData {
        public float[][][] path { get; set;}
        public bool closed {get; set;}
        public float[] pathPosition { get; set;}
        public float[] pathRotation { get; set;}
        public float[] pathNormal { get; set;}
        public float pathFraction { get; set;}
        public string rotationConstraintType { get; set;}
        public float maxFrictionForce {get; set;}
        public Motor motor1 { get; set;}
    }
    public class PulleyConstraint: JoltConstraintData {
        public string space { get; set;}
        public float[] bodyPoint1 { get; set;}
        public float[] bodyPoint2 { get; set;}
        public float[] fixedPoint1 { get; set;}
        public float[] fixedPoint2 { get; set;}
        public float ratio {get; set;}
        public float minLength {get; set;}
        public float maxLength {get; set;}
    }
#endif