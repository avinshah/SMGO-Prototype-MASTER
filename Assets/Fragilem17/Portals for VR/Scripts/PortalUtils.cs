using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Fragilem17.MirrorsAndPortals
{
    public class PortalUtils 
    {
        public enum DebugColors
        {
            Info,
            Warn,
            Error
        }

        public static string Colorize(string text, DebugColors color, bool bold = false)
        {
            string c = "00BC0E";
            if (color == DebugColors.Error)
            {
                c = "BE0000";
            }
            else if (color == DebugColors.Warn)
            {
                c = "FFB900";
            }

            return "<color=#" + c + ">" + (bold ? "<b>" : "") + text + (bold ? "</b>" : "") + "</color>";
        }

        // taken from http://www.terathon.com/code/oblique.html
        public static void MakeProjectionMatrixOblique(ref Matrix4x4 matrix, Vector4 clipPlane)
        {
            Vector4 q;

            // Calculate the clip-space corner point opposite the clipping plane
            // as (sgn(clipPlane.x), sgn(clipPlane.y), 1, 1) and
            // transform it into camera space by multiplying it
            // by the inverse of the projection matrix

            q.x = (sgn(clipPlane.x) + matrix[8]) / matrix[0];
            q.y = (sgn(clipPlane.y) + matrix[9]) / matrix[5];
            q.z = -1.0F;
            q.w = (1.0F + matrix[10]) / matrix[14];

            // Calculate the scaled plane vector
            Vector4 c = clipPlane * (2.0F / Vector3.Dot(clipPlane, q));

            // Replace the third row of the projection matrix
            matrix[2] = c.x;
            matrix[6] = c.y;
            matrix[10] = c.z + 1.0F;
            matrix[14] = c.w;
        }

        // Extended sign: returns -1, 0 or 1 based on sign of a
        private static float sgn(float a)
        {
            if (a > 0.0f) return 1.0f;
            if (a < 0.0f) return -1.0f;
            return 0.0f;
        }

        // Given position/normal of the plane, calculates plane in camera space.
        public static Vector4 CameraSpacePlane(Matrix4x4 worldToCameraMatrix, Vector3 pos, Vector3 normal, float sideSign, float clippingPlaneOffset)
        {
            Vector3 offsetPos = pos + normal * clippingPlaneOffset;
            Vector3 cpos = worldToCameraMatrix.MultiplyPoint(offsetPos);
            Vector3 cnormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        /*
        // Calculates reflection matrix around the given plane
        public static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }
        */
    }

    [Serializable]
    public class SerializableCurve
    {
        public SerializableKeyframe[] keys;
        public string postWrapMode;
        public string preWrapMode;

        [Serializable]
        public class SerializableKeyframe
        {
            public Single inTangent;
            public Single inWeight;
            public Single outTangent;
            public Single outWeight;
            public Int32 weightedMode;
            //public Int32 tangentMode;
            public Single time;
            public Single value;

            public SerializableKeyframe(Keyframe original, int index)
            {
                inTangent = original.inTangent;
                inWeight = original.inWeight;

                outTangent = original.outTangent;
                outWeight = original.outWeight;

                weightedMode = (int)original.weightedMode;
                //tangentMode = original.tangentMode;

                time = original.time;
                value = original.value;
            }
        }

        public SerializableCurve(AnimationCurve original)
        {
            postWrapMode = getWrapModeAsString(original.postWrapMode);
            preWrapMode = getWrapModeAsString(original.preWrapMode);
            keys = new SerializableKeyframe[original.length];
            for (int i = 0; i < original.keys.Length; i++)
            {
                keys[i] = new SerializableKeyframe(original.keys[i], i);
            }
        }

        public AnimationCurve toCurve()
        {
            AnimationCurve res = new AnimationCurve();
            res.postWrapMode = getWrapMode(postWrapMode);
            res.preWrapMode = getWrapMode(preWrapMode);
            Keyframe[] newKeys = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                SerializableKeyframe aux = keys[i];
                Keyframe newK = new Keyframe();
                newK.inTangent = aux.inTangent;
                newK.inWeight = aux.inWeight;
                newK.outTangent = aux.outTangent;
                newK.outWeight = aux.outWeight;
                //newK.tangentMode = aux.tangentMode;
                newK.weightedMode = (WeightedMode)aux.weightedMode;
                newK.time = aux.time;
                newK.value = aux.value;
                newKeys[i] = newK;
            }
            res.keys = newKeys;
            return res;
        }

        private WrapMode getWrapMode(String mode)
        {
            if (mode.Equals("Clamp"))
            {
                return WrapMode.Clamp;
            }
            if (mode.Equals("ClampForever"))
            {
                return WrapMode.ClampForever;
            }
            if (mode.Equals("Default"))
            {
                return WrapMode.Default;
            }
            if (mode.Equals("Loop"))
            {
                return WrapMode.Loop;
            }
            if (mode.Equals("Once"))
            {
                return WrapMode.Once;
            }
            if (mode.Equals("PingPong"))
            {
                return WrapMode.PingPong;
            }
            Debug.LogError("Wat is this wrap mode???");
            return WrapMode.Default;
        }

        private string getWrapModeAsString(WrapMode mode)
        {
            if (mode.Equals(WrapMode.Clamp))
            {
                return "Clamp";
            }
            if (mode.Equals(WrapMode.ClampForever))
            {
                return "ClampForever";
            }
            if (mode.Equals(WrapMode.Default))
            {
                return "Default";
            }
            if (mode.Equals(WrapMode.Loop))
            {
                return "Loop";
            }
            if (mode.Equals(WrapMode.Once))
            {
                return "Once";
            }
            if (mode.Equals(WrapMode.PingPong))
            {
                return "PingPong";
            }
            Debug.LogError("Wat is this wrap mode???");
            return "f you";
        }
    }
}
