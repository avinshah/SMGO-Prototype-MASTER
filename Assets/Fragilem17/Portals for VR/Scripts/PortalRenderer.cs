using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fragilem17.MirrorsAndPortals
{
    [ExecuteInEditMode]
    public class PortalRenderer : MonoBehaviour
    {
        public static List<PortalRenderer> portalRendererInstances;

        [Tooltip("The source material, disable and re-enable this component if you make changes to the material")]
        public List<Portal> Portals;

        [Tooltip("How many times can this surface reflect back onto the recursiveSurface.\nFrom 1 till the texturememory runs out.")]
        [MinAttribute(1)]
        public int recursions = 2;

        
        [Space(10)]
        [Header("Quality Settings")]

        [Tooltip("Uncheck useScreenScaleFactor to enter your own values.\nSquare values like 1024x1024 seem to render faster.")]
        public Vector2 textureSize = Vector2.one * 512;

        public bool useScreenScaleFactor = false;

        [Range(0.01f, 1f)]
        public float screenScaleFactor = 0.5f;

        [Space(5)]

        [Tooltip("Default and ARGB32 are generally good\nDefault HDR and ARGBHalf get rid of banding but some visual artifacts in recursions. ARGB64 is too heavy with no advantages to use it.")]
        public RenderTextureFormat _renderTextureFormat = RenderTextureFormat.Default;

        public AA antiAliasing = AA.Low;

        public bool disablePixelLights = true;


        [Tooltip("In VR this should probably always be 0")]
        [MinAttribute(0)]
        public int framesNeededToUpdate = 0;


        [Space(10)] // 10 pixels of spacing here.

        [Header("Other")]


        [Tooltip("The layer mask of the reflection camera")]
        public LayerMask RenderTheseLayers = -1;


        [Tooltip("Off is fast")]
        public CameraOverrideOption OpaqueTextureMode = CameraOverrideOption.Off;
        [Tooltip("Off is fast")]
        public CameraOverrideOption DepthTextureMode = CameraOverrideOption.Off;

        public bool RenderShadows = false;
        [Tooltip("As most PP is on top of your entire screen, it makes no sense to render PP in the reflection, and then again on the whole screen, so keep it off unless you know why you're turning it on.")]
        public bool RenderPostProcessing = false;


        [Space(10)]
        [Header("Events")]
        public UnityEvent onStartRendering;
        public UnityEvent onFinishedRendering;


        private List<PooledPortalTexture> _pooledTextures = new List<PooledPortalTexture>();

        private static Dictionary<Camera, Camera> _reflectionCameras = new Dictionary<Camera, Camera>();
        private static Dictionary<Camera, UniversalAdditionalCameraData> _UacPerCameras = new Dictionary<Camera, UniversalAdditionalCameraData>();

        private static InputDevice _centerEye;
        private static float _IPD = 0;
        private static Vector3 _leftEyePosition;
        private static Vector3 _rightEyePosition;

        private static Camera _reflectionCamera;

        private int _frameCounter = 0;

        private RenderTextureFormat _oldRenderTextureFormat = RenderTextureFormat.DefaultHDR;
        private AA _oldAntiAliasing = AA.Low;
        private int _oldTextureSize = 0;
        private bool _oldUseScreenScaleFactor = true;
        private float _oldScreenScaleFactor = 0.5f;

        private bool _isMultipass = true;
        private bool _UacAllowXRRendering = true;

        private List<CameraPortalMatrices> cameraMatricesInOrder = new List<CameraPortalMatrices>();

        private static PortalRenderer _master;
        private UniversalAdditionalCameraData _uacRenderCam;
        private UniversalAdditionalCameraData _uacReflectionCam;

        [Space(10)]
        [Header("Beta Features (read tooltip!)")]

        [Tooltip("Unity has a bug where an oblique occlusion culling matrix is not used correctly. (Items will be culled when the angle towards the mirror gets higher) The option is here already in the hopes that in a future Unity version you can turn it on. If things start flickering, turn it off!")]
        public bool UseOcclusionCulling = false;

        [Tooltip("When checked, the reflection will stop rendering but the materials will still update their position and blending")]
        public bool disableRenderingWhileStillUpdatingMaterials = false;


        private Portal _p;
        private bool _frustumIntersectsWithPortal = false;
        private Vector3[] _frustumCornersLeft = new Vector3[4];
        private Vector3[] _frustumCornersRight = new Vector3[4];

        private Mesh _clippingPlaneMeshLeft;
        private GameObject _clippingPlaneLeft;
        private Mesh _clippingPlaneMeshRight;
        private GameObject _clippingPlaneRight;
        private Material _clippingPlaneMaterial;

        private Portal _closestPortal;

#if UNITY_EDITOR_OSX
        [Tooltip("When checked, in Unity for MacOSX, the console will be spammed with a message each time a mirror renders, this is a workarround to a Unity Bug that instantly crashes the editor. (disable at your own peril)")]
        public bool enableMacOSXTemporaryLogsToAvoidCrashingTheEditor = true;
#endif

        public enum AA
        {
            None = 1,
            Low = 2,
            Medium = 4,
            High = 8
        }

        private void OnEnable()
        {
            Application.targetFrameRate = -1;

            if (portalRendererInstances == null)
            {
                portalRendererInstances = new List<PortalRenderer>();
            }
            portalRendererInstances.Add(this);

            RenderPipeline.beginCameraRendering += UpdateCamera;
            CreateClippingPlane();
        }

        private void CreateClippingPlane()
        { 
            if (!_clippingPlaneMeshLeft)
            {
                _clippingPlaneLeft = new GameObject("Clipping Plane for Portals Left " + name, typeof(MeshRenderer), typeof(MeshFilter));
                _clippingPlaneLeft.hideFlags = HideFlags.HideAndDontSave;

                _clippingPlaneMeshLeft = new Mesh();
                Vector3[] points = new Vector3[5];
                points[0] = Vector3.zero;
                points[1] = Vector3.zero;
                points[2] = Vector3.zero;
                points[3] = Vector3.zero;
                points[4] = Vector3.zero;

                int[] triangles = { 
                    0, 1, 2, 
                    0, 3, 1, 
                    0, 4, 3
                };

                _clippingPlaneMeshLeft.vertices = points;
                _clippingPlaneMeshLeft.triangles = triangles;

                MeshFilter f = _clippingPlaneLeft.GetComponent<MeshFilter>();
                f.sharedMesh = _clippingPlaneMeshLeft;

                _clippingPlaneMaterial = new Material(Shader.Find("MirrorsAndPortals/PortalSurfaceLiteOffsetAlwaysOnTop"));
                _clippingPlaneMaterial.renderQueue = 3001;
                _clippingPlaneMaterial.SetFloat("_Ztest", (float)UnityEngine.Rendering.CompareFunction.Always);

                MeshRenderer r = _clippingPlaneLeft.GetComponent<MeshRenderer>();
                r.sharedMaterial = _clippingPlaneMaterial;
                r.allowOcclusionWhenDynamic = false;


                _clippingPlaneRight = new GameObject("Clipping Plane for Portals Right " + name, typeof(MeshRenderer), typeof(MeshFilter));
                _clippingPlaneRight.hideFlags = HideFlags.HideAndDontSave;

                _clippingPlaneMeshRight = new Mesh();
                Vector3[] pointsRight = new Vector3[5];
                points[0] = Vector3.zero;
                points[1] = Vector3.zero;
                points[2] = Vector3.zero;
                points[3] = Vector3.zero;
                points[4] = Vector3.zero;

                _clippingPlaneMeshRight.vertices = pointsRight;
                _clippingPlaneMeshRight.triangles = triangles;

                f = _clippingPlaneRight.GetComponent<MeshFilter>();
                f.sharedMesh = _clippingPlaneMeshRight;

                r = _clippingPlaneRight.GetComponent<MeshRenderer>();
                r.sharedMaterial = _clippingPlaneMaterial;
                r.allowOcclusionWhenDynamic = false;
            }
        }

        private void LateUpdate()
        {
            if (XRSettings.enabled)
            {
                _isMultipass = XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass;
                if (_isMultipass && _reflectionCameras.Count > 0)
                {
                    foreach (Portal portal in Portals)
                    {
                        portal.PortalSurface.UpdatePositionsInMaterial(Camera.main.transform.position, Camera.main.transform.right);
                    }
                }
            }
        }

        private void UpdateCamera(UnityEngine.Rendering.ScriptableRenderContext src, Camera renderCamera)
        {
            bool renderNeeded = false;
#if UNITY_EDITOR
            // only render the first sceneView (so we can see debug info in a second sceneView)
            //int index = Mathf.Clamp(SceneViewIndex, 0, SceneView.sceneViews.Count - 1);
            //SceneView view = SceneView.sceneViews[index] as SceneView;
            renderNeeded = renderCamera.tag == "MainCamera" || renderCamera.tag == "SpectatorCamera" || (renderCamera.cameraType == CameraType.SceneView && renderCamera.name.IndexOf("Preview Camera") == -1); //  && view.camera == renderCamera
#else
            renderNeeded = renderCamera.tag == "MainCamera" || renderCamera.tag == "SpectatorCamera";
#endif

            if (!renderNeeded)
            {
                if (_master == this) { _master = null; }
                return;
            }

            if (!enabled || !renderCamera)
            {
                if (_master == this) { _master = null; }
                return;
            }

            if (Portals == null || Portals.Count == 0)
            {
                if (_master == this) { _master = null; }
                return;
            }

            if (_frameCounter > 0)
            {
                _frameCounter--;
                return;
            }
            _frameCounter = framesNeededToUpdate;


            if (_clippingPlaneLeft && renderCamera.cameraType != CameraType.SceneView)
            {
                // reset this cashed property
                _frustumIntersectsWithPortal = false;
                //Debug.Log("Hide clipping for: " + renderCamera.name);
                _clippingPlaneLeft.SetActive(false);
                if (_clippingPlaneRight)
                {
                    _clippingPlaneRight.SetActive(false);
                }
            }

            _closestPortal = null;
            float minDistance = float.MaxValue;
            if (renderCamera.cameraType != CameraType.SceneView)
            {
                for (int i = 0; i < Portals.Count; i++)
                {
                    _p = Portals[i];
                    if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                    {
                        float myDistance = Vector3.Distance(renderCamera.transform.position, _p.PortalSurface.ClosestPointOnBounds(renderCamera.transform.position));
                        //Debug.Log("myDistance: " + myDistance);
                        if (myDistance < minDistance)
                        {
                            minDistance = myDistance;
                            _closestPortal = _p;
                        }
                    }
                }
            }

            Portal ignoreCullingForPortal = null;
            if (minDistance < renderCamera.nearClipPlane)
            {
                //Debug.Log("minDistance: " + minDistance);
                //we're really near a portal!
                ignoreCullingForPortal = _closestPortal;
            }


            //Debug.Log("START SEARCH !! Portal Cameras on Root: " + name + " : " + renderCamera.name + " : " + Portals.Count);
            /*if (renderCamera.cameraType != CameraType.SceneView  && name == "DifferentScaledPortalsRenderer")
            {
                Debug.Log("START SEARCH !! Portal Cameras on Root: " + name + " : " + renderCamera.name + " : " + Portals.Count);
            }*/

            // check the distance          
            renderNeeded = false;
            for (int i = 0; i < Portals.Count; i++)
            {
                _p = Portals[i];
                if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                {
                    // the offset here should normally be only _IPD/2, BUT! As the physics are might not run in sync with framerate
                    // the transporter might not transport in time, to get to the other side
                    // adding a bit more space so we have an extra frame or two before we deem the camera to not be visible anymore.
                    float nearClipOffset = (_IPD/2) + renderCamera.nearClipPlane + 0.05f;
                    renderNeeded = _p.PortalSurface.VisibleFromCamera(renderCamera, false, nearClipOffset, (ignoreCullingForPortal == _p)) || renderNeeded;
                    /*if (renderNeeded)
                    {
                        Debug.Log("renderNeeded for portal " + _p.name + ", cam:" + renderCamera.name + " : "   + renderNeeded + " : " + ((_IPD / 2f) + renderCamera.nearClipPlane + 0.05f));
                    }*/
                    if (renderNeeded) {
                        break;
                    }
                }
            }            

            if (!renderNeeded)
            {
                if (_master == this) { _master = null; }
                return;
            }


            if (disableRenderingWhileStillUpdatingMaterials && cameraMatricesInOrder != null)
            {
                for (int i = 0; i < Portals.Count; i++)
                {
                    _p = Portals[i];
                    if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                    { 
                        float myDistance = Vector3.Distance(renderCamera.transform.position, _p.PortalSurface.ClosestPointOnBounds(renderCamera.transform.position));
                        _p.PortalSurface.UpdateMaterial(Camera.StereoscopicEye.Left, null, this, 1, myDistance);
                    }
                }
                if (_master == this) { _master = null; }
                return;
            }

            if (!_master) { _master = this; }


            CreatePortalCameras(renderCamera, out _reflectionCamera);

            _reflectionCamera.CopyFrom(renderCamera);
            _reflectionCamera.cullingMask = RenderTheseLayers.value;

#if UNITY_2020_3_OR_NEWER
            GetUACData(renderCamera, out _uacRenderCam);
            GetUACData(_reflectionCamera, out _uacReflectionCam);
            if (_uacRenderCam != null)
            {
                _UacAllowXRRendering = _uacRenderCam.allowXRRendering;

                _uacReflectionCam.requiresColorOption = OpaqueTextureMode;
                _uacReflectionCam.requiresDepthOption = DepthTextureMode;
                _uacReflectionCam.renderPostProcessing = RenderPostProcessing;
                _uacReflectionCam.renderShadows = RenderShadows;
            }
            else
            {
                _UacAllowXRRendering = true;
            }
#endif

            if (XRSettings.enabled && (_master == this) && _UacAllowXRRendering)
            {
                // get the IPD
                _centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                _centerEye.TryGetFeatureValue(CommonUsages.leftEyePosition, out _leftEyePosition);
                _centerEye.TryGetFeatureValue(CommonUsages.rightEyePosition, out _rightEyePosition);

                _IPD = Vector3.Distance(_leftEyePosition, _rightEyePosition) * renderCamera.transform.lossyScale.x;
            }

            _reflectionCamera.transform.localScale = Vector3.one * renderCamera.transform.lossyScale.x;

            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                Vector3 originalPos = renderCamera.transform.position;
                renderCamera.transform.position -= (renderCamera.transform.right * _IPD / 2f);
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                renderCamera.transform.position = originalPos;

                _reflectionCamera.projectionMatrix = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            }
            else
            {
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                _reflectionCamera.projectionMatrix = renderCamera.projectionMatrix;
            }


            cameraMatricesInOrder.Clear();

            onStartRendering.Invoke();


            Dictionary<PortalSurface, Vector3> startScales = new Dictionary<PortalSurface, Vector3>();  
            
            RecusiveFindPortalsInOrder(renderCamera, cameraMatricesInOrder, 0, 1, Camera.StereoscopicEye.Left, ignoreCullingForPortal);
            RenderPortalCamera(src, _reflectionCamera, cameraMatricesInOrder, Camera.StereoscopicEye.Left);
            //Debug.Log("END RENDER!! " + name + " : " + renderCamera.name);

            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                Vector3 originalPos = renderCamera.transform.position;
                renderCamera.transform.position += (renderCamera.transform.right * _IPD / 2f);
                _reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                _reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix;
                renderCamera.transform.position = originalPos;

                _reflectionCamera.projectionMatrix = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                cameraMatricesInOrder.Clear();

                RecusiveFindPortalsInOrder(renderCamera, cameraMatricesInOrder, 0, 1, Camera.StereoscopicEye.Right, ignoreCullingForPortal);
                RenderPortalCamera(src, _reflectionCamera, cameraMatricesInOrder, Camera.StereoscopicEye.Right);

                for (int i = 0; i < Portals.Count; i++)
                {
                    _p = Portals[i];
                    if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                    {
                        _p.PortalSurface.TurnOffForceEye();
                    }
                }
            }
            else {
                if (_isMultipass)
                {
                    for (int i = 0; i < Portals.Count; i++)
                    {
                        _p = Portals[i];
                        if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                        {
                            _p.PortalSurface.ForceLeftEye();
                        }
                    }
                }
            }


            // find the closest portal and draw a clipping mesh
            if (renderCamera.cameraType != CameraType.SceneView)
            {
                if (_closestPortal != null && _clippingPlaneLeft)
                {
                    //Debug.Log("closestPortal: " + closestPortal);
                    //closestPortal.PortalSurface.CalcOffset();

                    Camera.MonoOrStereoscopicEye eye = Camera.MonoOrStereoscopicEye.Mono;
                    if (XRSettings.enabled && _UacAllowXRRendering)
                    {
                        eye = Camera.MonoOrStereoscopicEye.Left;
                    }

                    bool leftMeshDrawn = DrawClippingMesh(renderCamera, _closestPortal, eye);
                    bool rightMeshDrawn = false;
                    if (XRSettings.enabled && _UacAllowXRRendering)
                    {
                        rightMeshDrawn = DrawClippingMesh(renderCamera, _closestPortal, Camera.MonoOrStereoscopicEye.Right);
                    }

                    //Debug.Log("CLOSEST PORTAL " + closestPortal.name + " : "+ leftMeshDrawn);

                    if (_clippingPlaneMaterial && (leftMeshDrawn || rightMeshDrawn))
                    {
                        _clippingPlaneMaterial.SetColor("_FadeColor", _closestPortal.PortalSurface.BlendColor);

                        _clippingPlaneMaterial.SetFloat("_FadeColorBlend", _closestPortal.PortalSurface._currentDistanceBlend);
                        _clippingPlaneMaterial.SetTexture("_TexLeft", _closestPortal.PortalSurface._currentTexLeft);
                        if (XRSettings.enabled && _UacAllowXRRendering)
                        {
                            _clippingPlaneMaterial.SetTexture("_TexRight", _closestPortal.PortalSurface._currentTexRight);
                            _clippingPlaneMaterial.SetInt("_ForceEye", -1);
                        }
                        //Debug.Break();
                    }

                    if (!leftMeshDrawn)
                    {
                        //Debug.Log("Hide clipping 2 for: " + renderCamera.name);
                        _clippingPlaneLeft.SetActive(false);
                    }
                    if (!rightMeshDrawn)
                    {
                        _clippingPlaneRight.SetActive(false);
                    }
                }
            }

            onFinishedRendering.Invoke();
        }

        private bool FrustumIntersectsWithPortal(Camera renderCamera, Portal portal, ref Vector3[] frustumCorners, Camera.MonoOrStereoscopicEye eye)
        {
            //Debug.Log("FrustumIntersectsWithPortal? 1");
            if (!portal.PortalTransporter || !portal.PortalTransporter.MyCollider)
            {
                return false;
            }

            Plane p = new Plane(-portal.PortalSurface.transform.forward, portal.PortalSurface.transform.position);
            Vector3 closestPointOnPlane = p.ClosestPointOnPlane(renderCamera.transform.position);
            Vector3 dir = (closestPointOnPlane - renderCamera.transform.position);
            float facing = Vector3.Dot(dir, portal.PortalSurface.transform.forward);

            if (facing < -((_IPD/2) + renderCamera.nearClipPlane + 0.05f)) // + 0.05f
            {
                return false;
            }

            //Debug.Log("FrustumIntersectsWithPortal? " + renderCamera.name +" : "  + facing);
            // calculate the depth needed, NearClipping field and IPD
            Bounds frustumSquare;

            Vector3 worldSpaceCornerPos;

            float originalNearClipPlane = renderCamera.nearClipPlane;
            float scaleFactor = Math.Max((portal.transform.lossyScale.z / portal.OtherPortal.transform.lossyScale.z), (portal.OtherPortal.transform.lossyScale.z / portal.transform.lossyScale.z));
            //scaleFactor = Math.Max(1, scaleFactor);
            //Debug.Log("scaleFactor for: " + portal.name + " : " + scaleFactor);

            renderCamera.nearClipPlane = (originalNearClipPlane * scaleFactor);
            renderCamera.nearClipPlane += 0.01f;

            Vector3 originalPos = renderCamera.transform.position;
            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                if (eye == Camera.MonoOrStereoscopicEye.Left)
                {
                    renderCamera.transform.position -= (renderCamera.transform.right * _IPD / 2f);
                }
                else
                {
                    renderCamera.transform.position += (renderCamera.transform.right * _IPD / 2f);
                }
            }


            renderCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), (renderCamera.nearClipPlane) / renderCamera.transform.lossyScale.x, eye, frustumCorners);
            // make the frustumCorners worldSpace
            for (int i = 0; i < 4; i++)
            {
                frustumCorners[i] = renderCamera.transform.TransformPoint(frustumCorners[i]);
            }

            frustumSquare = new Bounds(frustumCorners[0], Vector3.one * 0.052f);
            for (int i = 1; i < 4; i++)
            {
                worldSpaceCornerPos = frustumCorners[i];
                frustumSquare.Encapsulate(new Bounds(worldSpaceCornerPos, Vector3.one * 0.052f));
            }
            // 10 cm from the back of your head, if you're moving backwards
            frustumSquare.Encapsulate(new Bounds(renderCamera.transform.position, Vector3.one * 0.2f)); 

            renderCamera.transform.position = originalPos;

            renderCamera.nearClipPlane = originalNearClipPlane;

            _frustumIntersectsWithPortal = (portal.PortalTransporter.MyCollider && portal.PortalTransporter.MyCollider.bounds.Intersects(frustumSquare));
            //Debug.Log("FrustumIntersectsWithPortal? 3 " + portal.name + " : " + _frustumIntersectsWithPortal);
            return _frustumIntersectsWithPortal;
        }


        private bool DrawClippingMesh(Camera renderCamera, Portal portal, Camera.MonoOrStereoscopicEye eye)
        {
            Vector3[] frustumCorners = _frustumCornersLeft;
            if (eye == Camera.MonoOrStereoscopicEye.Right)
            {
                frustumCorners = _frustumCornersRight;
            }
            //Debug.Log("DrawClippingMesh for " + renderCamera.name);

            

            if (FrustumIntersectsWithPortal(renderCamera, portal, ref frustumCorners, eye))
            {
                GameObject clippingPlane = _clippingPlaneLeft;
                Mesh clippingPlaneMesh = _clippingPlaneMeshLeft;
                if (eye == Camera.MonoOrStereoscopicEye.Right)
                {
                    clippingPlane = _clippingPlaneRight;
                    clippingPlaneMesh = _clippingPlaneMeshRight;
                }

                //Debug.Log("show clipping plane: " + renderCamera.name);
                clippingPlane.SetActive(true);

                Plane p = new Plane(-portal.PortalSurface.transform.forward, portal.PortalSurface.transform.position);
                Vector3 worldSpaceCornerPos;

                Vector3 closestPointOnPlane;
                Vector3 dir;
                float facing;
                Vector3[] vertexes = new Vector3[5];

                float cornerToCornerDistance;
                Vector3 intersectionPoint = Vector3.zero;

                // find the first corner that intersects
                int vertexIndex = 0;
                for (int i = 0; i < 4; i++)
                {
                    worldSpaceCornerPos = frustumCorners[i];

                    //Debug.Log(closestPortal.PortalTransporter.MyCollider.bounds);
                    closestPointOnPlane = p.ClosestPointOnPlane(worldSpaceCornerPos);
                    dir = (closestPointOnPlane - worldSpaceCornerPos);
                    facing = Vector3.Dot(dir, portal.PortalSurface.transform.forward);
                    //Debug.Log(renderCamera.name + " dir: " + dir.magnitude + " : " + facing +" : "+ reason);
                    if (facing < 0)
                    {
                        // this corner lies behind the plane, thus is a corner of our mesh
                        vertexes[vertexIndex] = worldSpaceCornerPos;
                        vertexIndex++;
                    }
                    else
                    { 
                        // this corner is in front, so it's possibly making 2 corners of our mesh

                        // we intersect with the portal, find the intersection point towards the next corner
                        int prevI = i - 1;
                        prevI = prevI == -1 ? 3 : prevI;

                        int nextI = i + 1;
                        nextI = nextI == 4 ? 0 : nextI;

                        Vector3 prevCornerPos = frustumCorners[prevI];
                        Vector3 nextCornerPos = frustumCorners[nextI];
                        float enterPointDistance;
                        Ray rayFromCornerToCorner = new Ray(worldSpaceCornerPos, prevCornerPos - worldSpaceCornerPos);

                        //Debug.DrawRay(worldSpaceCornerPos, rayFromCornerToCorner.direction, Color.magenta, 0.3f);

                        if (p.Raycast(rayFromCornerToCorner, out enterPointDistance))
                        {
                            cornerToCornerDistance = Vector3.Distance(worldSpaceCornerPos, prevCornerPos);

                            //enterPointDistance = Mathf.Min(enterPointDistance, cornerToCornerDistance);

                            if (enterPointDistance < cornerToCornerDistance)
                            {
                                //Debug.Log("prevPoint intersection: " + i + " : " + cornerToCornerDistance + " : " + enterPointDistance);
                                intersectionPoint = rayFromCornerToCorner.origin + (rayFromCornerToCorner.direction * (enterPointDistance + 0.000f));
                                vertexes[vertexIndex] = intersectionPoint;
                                vertexIndex++;
                                //Debug.DrawLine(worldSpaceCornerPos, intersectionPoint, Color.yellow, 0.2f);
                            }
                            //Debug.Log("found1: " + enterPointDistance);
                        }

                        rayFromCornerToCorner = new Ray(worldSpaceCornerPos, nextCornerPos - worldSpaceCornerPos);

                        //Debug.DrawRay(worldSpaceCornerPos, rayFromCornerToCorner.direction, Color.magenta, 0.3f);

                        if (p.Raycast(rayFromCornerToCorner, out enterPointDistance))
                        {
                            cornerToCornerDistance = Vector3.Distance(worldSpaceCornerPos, nextCornerPos);
                            //enterPointDistance = Mathf.Min(enterPointDistance, cornerToCornerDistance);
                            if (enterPointDistance < cornerToCornerDistance)
                            {
                                //Debug.Log("nextPoint intersection: " + i + " : " + cornerToCornerDistance + " : " + enterPointDistance);
                                intersectionPoint = rayFromCornerToCorner.origin + (rayFromCornerToCorner.direction * (enterPointDistance + 0.000f));
                                vertexes[vertexIndex] = intersectionPoint;
                                vertexIndex++;
                                //Debug.DrawLine(worldSpaceCornerPos, intersectionPoint, Color.green, 0.2f);
                            }
                            //Debug.Log("found2: " + enterPointDistance);
                        }
                    }
                }

                if (clippingPlane && vertexIndex >= 3)
                {
                    //Vector3[] v = new Vector3[vertexes.Count];
                    //vertexes.CopyTo(v);
                    clippingPlane.transform.SetPositionAndRotation(renderCamera.transform.position + (renderCamera.transform.forward * (renderCamera.nearClipPlane + 0.01f)), renderCamera.transform.rotation);

                    Vector3[] vertices = clippingPlaneMesh.vertices;
                    //Debug.Log("Vertexes:" + vertexes.Length + " : " + vertexIndex);

                    Vector3 localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[0]);
                    vertices[1] = localSpace;

                    localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[1]);
                    vertices[2] = localSpace;

                    localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[2]);
                    vertices[0] = localSpace;

                    if (vertexIndex == 3)
                    {
                        vertices[3] = localSpace;
                        vertices[4] = localSpace;
                    }

                    if (vertexIndex == 4)
                    {
                        localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[3]);
                        vertices[3] = localSpace;
                        vertices[4] = localSpace;
                    }

                    if (vertexIndex >= 5)
                    {
                        localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[4]);
                        vertices[3] = localSpace;
                        localSpace = clippingPlane.transform.InverseTransformPoint(vertexes[3]);
                        vertices[4] = localSpace;
                    }


                    clippingPlaneMesh.vertices = vertices;
                    //clippingPlaneMesh.RecalculateBounds();
                    return true;
                }
            }

            return false;
        }


        private float FindNearClippingOffset(Camera renderCamera, Portal portal, string reason)
        {
            Camera.MonoOrStereoscopicEye eye = Camera.MonoOrStereoscopicEye.Mono;
            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                eye = Camera.MonoOrStereoscopicEye.Left;
            }
            bool intersectsLeft = FrustumIntersectsWithPortal(renderCamera, portal, ref _frustumCornersLeft, eye);
            bool intersectsRight = false;
            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                intersectsRight = FrustumIntersectsWithPortal(renderCamera, portal, ref _frustumCornersRight, Camera.MonoOrStereoscopicEye.Right);
            }


            if (intersectsLeft || intersectsRight) { 
             
                Plane p = new Plane(-portal.PortalSurface.transform.forward, portal.PortalSurface.transform.position);
                Vector3 worldSpaceCornerPos;

                //float d = Vector3.Distance(renderCamera.transform.position, _frustumCornersLeft[0]);

                Vector3 closestPointOnPlane;
                Vector3 dir;
                float facing;
                float maxDist = 0f;

                for (int i = 0; i < 4; i++)
                {
                    worldSpaceCornerPos = _frustumCornersLeft[i];

                    //Debug.Log(closestPortal.PortalTransporter.MyCollider.bounds);
                    closestPointOnPlane = p.ClosestPointOnPlane(worldSpaceCornerPos);
                    dir = (closestPointOnPlane - worldSpaceCornerPos);
                    facing = Vector3.Dot(dir, portal.PortalSurface.transform.forward);
                    //Debug.Log(renderCamera.name + " dir: " + dir.magnitude + " : " + facing +" : "+ reason);
                    if (facing < 0)
                    {
                        //Debug.DrawRay(worldSpaceCornerPos, dir, Color.red, 0.2f, false);
                        maxDist = Mathf.Max(maxDist, dir.magnitude);
                    }
                    else
                    {
                        //Debug.DrawRay(worldSpaceCornerPos, dir, Color.blue, 0.2f, true);
                    }
                }

                if (XRSettings.enabled && _UacAllowXRRendering)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        worldSpaceCornerPos = _frustumCornersRight[i];
                        closestPointOnPlane = p.ClosestPointOnPlane(worldSpaceCornerPos);

                        dir = (closestPointOnPlane - worldSpaceCornerPos);
                        facing = Vector3.Dot(dir, portal.PortalSurface.transform.forward);
                        //Debug.DrawRay(worldSpaceCornerPos, dir, Color.blue, 0.2f, true);
                        //Debug.Log(renderCamera.name + " dir: " + dir.magnitude + " : " + facing);
                        if (facing < 0)
                        {
                            maxDist = Mathf.Max(maxDist, dir.magnitude);
                        }
                    }
                }

                return maxDist;
            }

            return 0;
        }

        private void RecusiveFindPortalsInOrder(Camera renderCamera, List<CameraPortalMatrices> cameraPortalMatricesInOrder,
            float previousDistance, int depth, Camera.StereoscopicEye eye, Portal ignoreCullingForPortal,
            PortalSurface parentSurface = null,
            PortalSurface parentsParentSurface = null,
            PortalSurface parentsParentsParentSurface = null,
            PortalSurface parentsParentsParentsParentSurface = null)
        {
            _reflectionCamera.ResetWorldToCameraMatrix();

            // look one deeper to know which deepest mirrors to turn dark
            if (depth > recursions + 1)
            {
                return;
            }

            Vector3 eyePosition = _reflectionCamera.transform.position;
            Quaternion eyeRotation = _reflectionCamera.transform.rotation;
            Matrix4x4 projectionMatrix = _reflectionCamera.projectionMatrix;

            if (XRSettings.enabled && _UacAllowXRRendering)
            {
                projectionMatrix = _reflectionCamera.GetStereoProjectionMatrix(eye);
            }

            Vector3 planeIntersection = Vector3.zero;
            //Debug.DrawLine(_reflectionCamera.transform.position, _reflectionCamera.transform.position + _reflectionCamera.transform.forward, Color.red, 0, false);

            float myDistance;

            PortalSurface portalSurface;
            for (int i = 0; i < Portals.Count; i++)
            {
                _p = Portals[i];
                if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                {
                    portalSurface = _p.PortalSurface;
                    if (portalSurface != null && _p.OtherPortal != null)
                    {
                        float nearClipOffset = 0;
                        if (depth == 1)
                        {
                            nearClipOffset = FindNearClippingOffset(_reflectionCamera, _p, "recusiveFind"); // (_IPD/2) +_reflectionCamera.nearClipPlane + 0.05f; //
                        }
                        //Debug.Log("gonna test VisibleFromCamera " + _p.name + " : " + depth + " : "+ _reflectionCamera.name);
                        if (portalSurface.VisibleFromCamera(_reflectionCamera, true, nearClipOffset, (ignoreCullingForPortal == _p)))
                        {
                            myDistance = previousDistance + Vector3.Distance(eyePosition, portalSurface.ClosestPointOnBounds(eyePosition));
                            //Debug.Log(_p.name +" : "+ depth + " : " +nearClipOffset + " previousDistance: " + previousDistance + " myDistance: " + myDistance);

                            if (myDistance <= portalSurface.maxRenderingDistance)
                            {
                                //myDistance = previousDistance + Vector3.Distance(eyePosition, portalSurface.ClosestPointOnPlane(eyePosition));
                                if (!portalSurface.Portal || !portalSurface.Portal.OtherPortal || !portalSurface.Portal.OtherPortal.PortalSurface)
                                {
                                    break;
                                }

                                Transform inTransform = portalSurface.transform;
                                Transform outTransform = portalSurface.Portal.OtherPortal.PortalSurface.transform;

                                Transform reflectionCameraTransform = _reflectionCamera.transform;
                                reflectionCameraTransform.position = eyePosition;
                                reflectionCameraTransform.rotation = eyeRotation;

                                // Position the camera behind the other portal.
                                Vector3 relativePos = inTransform.InverseTransformPoint(reflectionCameraTransform.position);
                                relativePos = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativePos;
                                reflectionCameraTransform.position = outTransform.TransformPoint(relativePos);

                                // Rotate the camera to look through the other portal.
                                Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * reflectionCameraTransform.rotation;
                                relativeRot = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativeRot;
                                reflectionCameraTransform.rotation = outTransform.rotation * relativeRot;

                                _reflectionCamera.transform.position = reflectionCameraTransform.position;
                                _reflectionCamera.transform.rotation = reflectionCameraTransform.rotation;

                                // Set the camera's oblique view frustum.
                                Plane p = new Plane(-outTransform.forward, outTransform.position);
                                //Debug.Log("p.distance=: " + p.distance);
                                Vector4 clipPlaneWorldSpace = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance - portalSurface.clippingPlaneOffset);
                                Vector4 clipPlaneCameraSpace =
                                    Matrix4x4.Transpose(Matrix4x4.Inverse(_reflectionCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;


                                Matrix4x4 newProjectionMatrix = _reflectionCamera.projectionMatrix;
                                if (XRSettings.enabled && _UacAllowXRRendering)
                                {
                                    newProjectionMatrix = _reflectionCamera.GetStereoProjectionMatrix(eye);
                                }

                                // only make the projectionMatrix oblique when we're still in front of the mirror with the reflectionCam
                                // makes no sense to do it behind the portal, also stay atleast the camera's nearplane away!

                                // calculate the direction to the plane
                                Vector3 closestPointOnPlane = p.ClosestPointOnPlane(_reflectionCamera.transform.position);
                                Vector3 dirToPlane = _reflectionCamera.transform.position - closestPointOnPlane;
                                
                                float sideOfTheMirrorWithCenterEye = Vector3.Dot(-1 * outTransform.forward, dirToPlane);

                                // todo: stop making it oblique when we're near, we would get glitches
                                //Debug.Log("find: " + renderCamera.name + " behind mirror? " + _p.name + " center: " + sideOfTheMirrorWithCenterEye + " depth: " + depth);

                                // todo: if we're outside the portalSurface, looking at a steep angle, we would see clipping artifacts
                                // because we're removing the oblique < 0.8cm or something (weird glitches when we don't)
                                // however, the weird glitches do not occur if you're not INSIDE the portal.
                                // how do we know this here?

                                if (sideOfTheMirrorWithCenterEye < -(renderCamera.nearClipPlane + 0.005f)) // (_IPD/2)
                                {
                                    PortalUtils.MakeProjectionMatrixOblique(ref newProjectionMatrix, clipPlaneCameraSpace);
                                }

                                Matrix4x4 newWorldToCameraMatrix = _reflectionCamera.worldToCameraMatrix;
                                Matrix4x4 newCullingMatrix = newProjectionMatrix * newWorldToCameraMatrix;
                                if (UseOcclusionCulling)
                                {
                                    // offset the plane of bit forward so we have a better chanse to not cull in just in front of the mirror
                                    Matrix4x4 newProjectionMatrixForClipping = newProjectionMatrix;
                                    Vector4 clipPlaneForCulling = PortalUtils.CameraSpacePlane(newWorldToCameraMatrix, outTransform.position, -outTransform.forward, 1.0f, portalSurface.clippingPlaneOffset + 0.4f);
                                    PortalUtils.MakeProjectionMatrixOblique(ref newProjectionMatrixForClipping, clipPlaneForCulling);
                                    newCullingMatrix = newProjectionMatrixForClipping * newWorldToCameraMatrix;
                                }

                                Vector3 newPos = _reflectionCamera.transform.position;


                                //Debug.Log("Search Mirror Cameras seen by " + reflectionMs.name + " : " + depth);
                                RecusiveFindPortalsInOrder(renderCamera, cameraMatricesInOrder, myDistance, depth + 1, eye, ignoreCullingForPortal ,portalSurface, parentSurface, parentsParentSurface, parentsParentsParentSurface);

                                // we might have moved the reflection camera in a previous iteration
                                // reset it for the next 
                                _reflectionCamera.transform.position = eyePosition;
                                _reflectionCamera.transform.rotation = eyeRotation;
                                //_reflectionCamera.worldToCameraMatrix = worldToCameraMatrix;
                                _reflectionCamera.projectionMatrix = projectionMatrix;

                                //Debug.Log("Found Mirror: " + portalSurface.Portal.name + " depth: " + depth + " parent: " + parentSurface?.Portal.name +" : "+ nearClipOffset +" : " + myDistance);
                                cameraMatricesInOrder.Add(new CameraPortalMatrices(newProjectionMatrix, newWorldToCameraMatrix, newCullingMatrix, portalSurface, depth % 2 != 0, newPos, depth, myDistance, parentSurface, parentsParentSurface, parentsParentsParentSurface, parentsParentsParentsParentSurface));
                            }
                            else
                            {
                                //Debug.Log("to far: " + portalSurface.Portal.name + " : " + depth + " : " + myDistance + " : " + portalSurface.maxRenderingDistance);
                                cameraMatricesInOrder.Add(new CameraPortalMatrices(Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, portalSurface, true, Vector3.zero, recursions + 1, myDistance, parentSurface, parentsParentSurface, parentsParentsParentSurface, parentsParentsParentsParentSurface));
                            }
                        }
                    }
                }
            }

            //Debug.Log("stop search: " + depth + " : " + parentSurface?.name);
        }

        private void RenderPortalCamera(UnityEngine.Rendering.ScriptableRenderContext src, Camera reflectionCamera, List<CameraPortalMatrices> cameraMatricesInOrder, Camera.StereoscopicEye eye)
        {
            //Debug.Log("RenderMirrorCamera");

            // Optionally disable pixel lights for reflection
            int oldPixelLightCount = QualitySettings.pixelLightCount;
            if (disablePixelLights)
            {
                QualitySettings.pixelLightCount = 0;
            }

            PooledPortalTexture _ptex = null;
            // position and render the camera
            CameraPortalMatrices matrices = null;


            for (int i = 0; i < cameraMatricesInOrder.Count; i++)
            {
                matrices = cameraMatricesInOrder[i];

                if (matrices.depth >= recursions + 1)
                {
                    //Debug.Log(" render surface lite: " + matrices.mirrorSurface.name + " de: " + matrices.depth + " pa: " + matrices.parentMirrorSurface?.name + " di: " + matrices.distance);
                    // make it completely blended, no need to render these
                    matrices.mirrorSurface.UpdateMaterial(eye, null, this, matrices.depth, Mathf.Infinity);
                }
                else
                {
                    GetFreeTexture(out _ptex, eye);
                    _ptex.matrices = matrices;

                    //Debug.Log(" depth: " + matrices.depth + " render op: " + matrices.mirrorSurface.name + " VOOR parent: " + matrices.parentMirrorSurface?.name + " using tex: " + _ptex.texture.name + " parentsParent: "+ matrices.parentsParentMirrorSurface);

                    _ptex.liteLock = true;

                    if (matrices.parentMirrorSurface == null)
                    {
                        _pooledTextures.ForEach(pTex => {
                            pTex.liteLock = false;
                        });
                        _ptex.fullLock = true;
                    }
                                        
                    reflectionCamera.targetTexture = _ptex.texture;
                    reflectionCamera.worldToCameraMatrix = matrices.worldToCameraMatrix;
                    reflectionCamera.projectionMatrix = matrices.projectionMatrix;
                    reflectionCamera.transform.position = matrices.camPos;

                    //Debug.Log(" render surface heav: " + matrices.mirrorSurface.name + " de : " + matrices.depth + " pa: " + matrices.parentMirrorSurface?.name + " di: " + matrices.distance + " tex: " + _ptex.texture.name);

                    reflectionCamera.useOcclusionCulling = UseOcclusionCulling;
                    reflectionCamera.cullingMatrix = matrices.cullingMatrix;


#if UNITY_EDITOR_OSX
                    if(enableMacOSXTemporaryLogsToAvoidCrashingTheEditor){
                        Debug.Log(" a bug in Unity for MacOSX causes the editor to crash if this message is not here. Terribly sorry about this");
                    }
#endif

                    UniversalRenderPipeline.RenderSingleCamera(src, reflectionCamera);
                    matrices.mirrorSurface.UpdateMaterial(eye, _ptex.texture, this, matrices.depth, matrices.distance);

                    // reset the material to the one with the lowest depth
                    List<CameraPortalMatrices> li = cameraMatricesInOrder.FindAll(x => x.depth == matrices.depth
                        && x.depth == matrices.depth
                        && x.parentMirrorSurface == matrices.parentMirrorSurface
                        && x.parentsParentMirrorSurface == matrices.parentsParentMirrorSurface
                        && x.parentsParentsParentMirrorSurface == matrices.parentsParentsParentMirrorSurface
                        && x.parentsParentsParentsParentMirrorSurface == matrices.parentsParentsParentsParentMirrorSurface);
                    //Debug.Log("how many?" + li.Count);

                    if (li.Count > 0)
                    {
                        foreach (CameraPortalMatrices cm in li)
                        {
                            if (cm != matrices)
                            {
                                PooledPortalTexture p = _pooledTextures.Find(ptex => ptex.matrices.mirrorSurface == cm.mirrorSurface
                                    && ptex.matrices.parentMirrorSurface == cm.parentMirrorSurface
                                    && ptex.matrices.parentsParentMirrorSurface == cm.parentsParentMirrorSurface
                                    && ptex.matrices.parentsParentsParentMirrorSurface == cm.parentsParentsParentMirrorSurface
                                    && ptex.matrices.parentsParentsParentsParentMirrorSurface == cm.parentsParentsParentsParentMirrorSurface
                                    && ptex.matrices.depth == cm.depth && ptex.eye == eye);
                                if (p != null)
                                {
                                    cm.mirrorSurface.UpdateMaterial(eye, p.texture, this, cm.depth, cm.distance);
                                }
                            }
                        }
                    }


                    // turn on occlusionCulling even though there is an issue with the cullingMatrix for mirrors
                    // the RecusiveFindMirrorsInOrder will use VisibleFromCamera and that can early exit 
                    reflectionCamera.useOcclusionCulling = true;
                }
            }


            // break the textureLocks
            _pooledTextures.ForEach(pTex => {
                pTex.liteLock = false;
                pTex.fullLock = false;
            });

            // Restore pixel light count
            if (disablePixelLights)
            {
                QualitySettings.pixelLightCount = oldPixelLightCount;
            }
        }

        private void GetFreeTexture(out PooledPortalTexture textureOut, Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left)
        {
            PooledPortalTexture tex = _pooledTextures.Find(tex => !tex.fullLock && !tex.liteLock && tex.eye == eye);
            if (tex == null)
            {
                tex = new PooledPortalTexture();
                tex.eye = eye;
                _pooledTextures.Add(tex);

                // create the texture
                //Debug.Log("creating new pooledTexture: " + _pooledTextures.Count);

                if (useScreenScaleFactor && screenScaleFactor > 0)
                {
                    float scale = screenScaleFactor; // * (1f / depth);
                    textureSize = new Vector2(Screen.width * scale, Screen.height * scale);
                }

                //RenderTextureDescriptor desc = new RenderTextureDescriptor((int)textureSize.x, (int)textureSize.y, RenderTextureFormat.ARGB32, 1);
                RenderTextureDescriptor desc = new RenderTextureDescriptor((int)textureSize.x, (int)textureSize.y, _renderTextureFormat, 1);
                desc.useMipMap = false;
                desc.autoGenerateMips = false;

                desc.msaaSamples = (int)antiAliasing;

                tex.texture = RenderTexture.GetTemporary(desc);
                tex.texture.wrapMode = TextureWrapMode.Mirror;
                tex.texture.filterMode = FilterMode.Trilinear;
                tex.texture.anisoLevel = 9;
                tex.texture.name = "_Tex" + gameObject.name + "_" + _pooledTextures.Count;
                tex.texture.hideFlags = HideFlags.DontSave;
            }

            textureOut = tex;
        }

        private void Update()
        {
            if (Portals == null)
            {
                return;
            }
            foreach (Portal p in Portals) {
                p.MyRenderer = this;
            }

            if (_oldTextureSize != ((int)textureSize.x + (int)textureSize.y)
                || _oldScreenScaleFactor != screenScaleFactor
                || _oldAntiAliasing != antiAliasing
                || _oldRenderTextureFormat != _renderTextureFormat
                || _oldUseScreenScaleFactor != useScreenScaleFactor)
            {
                _oldUseScreenScaleFactor = useScreenScaleFactor;
                _oldAntiAliasing = antiAliasing;
                _oldRenderTextureFormat = _renderTextureFormat;
                _oldScreenScaleFactor = screenScaleFactor;
                _oldTextureSize = ((int)textureSize.x + (int)textureSize.y);

                foreach (PooledPortalTexture tex in _pooledTextures)
                {
                    DestroyImmediate(((RenderTexture)tex.texture));
                }
                _pooledTextures.Clear();

            }

            if (recursions > 8)
            {
                recursions = 8;

            }
        }
        private void GetUACData(Camera renderCamera, out UniversalAdditionalCameraData uac)
        {
            UniversalAdditionalCameraData uacOut;

            if (!_UacPerCameras.TryGetValue(renderCamera, out uacOut))
            {
                uacOut = renderCamera.GetComponent<UniversalAdditionalCameraData>();
                _UacPerCameras.Add(renderCamera, uacOut);
            }
            uac = uacOut;
        }

        private void CreatePortalCameras(Camera renderCamera, out Camera reflectionCamera)
        {
            reflectionCamera = null;

            // Camera for reflection
            Camera reflectionCam;
            _reflectionCameras.TryGetValue(renderCamera, out reflectionCam);

            if (reflectionCam == null)
            {
                //Debug.Log("new reflection camera for " + renderCamera.name);
                GameObject go = new GameObject("Portal Camera for " + renderCamera.name, typeof(Camera), typeof(Skybox));
                reflectionCamera = go.GetComponent<Camera>();
                reflectionCamera.useOcclusionCulling = true;
                reflectionCamera.enabled = false;
                reflectionCamera.transform.position = transform.position;
                reflectionCamera.transform.rotation = transform.rotation;
                reflectionCamera.gameObject.AddComponent<FlareLayer>();

                //reflectionCamera.clearFlags = CameraClearFlags.Nothing;
                //reflectionCamera.depthTextureMode = DepthTextureMode.None;

                GetUACData(renderCamera, out _uacRenderCam);
                //_uacRenderCam = renderCamera.GetComponent<UniversalAdditionalCameraData>();
                if (_uacRenderCam != null)
                {
                    _uacReflectionCam = reflectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    _uacReflectionCam.requiresColorOption = OpaqueTextureMode;
                    _uacReflectionCam.requiresDepthOption = DepthTextureMode;
                    _uacReflectionCam.renderPostProcessing = RenderPostProcessing;
                    _uacReflectionCam.renderShadows = RenderShadows;

#if UNITY_2020_3_OR_NEWER
                    _uacReflectionCam.allowXRRendering = _uacRenderCam.allowXRRendering;
#endif
                }

                go.hideFlags = HideFlags.DontSave;

                if (_reflectionCameras.ContainsKey(renderCamera))
                {
                    _reflectionCameras[renderCamera] = reflectionCamera;
                }
                else
                {
                    _reflectionCameras.Add(renderCamera, reflectionCamera);
                }
            }
            else
            {
                reflectionCamera = reflectionCam;
            }
        }


        private void OnDestroy()
        {
            OnDisable();
        }

        // Cleanup all the objects we possibly have created
        void OnDisable()
        {
            //Debug.Log("OnDisable");
            portalRendererInstances.Remove(this);
            RenderPipeline.beginCameraRendering -= UpdateCamera;

            if (_master == this)
            {
                _master = null;
            }

            if (!Application.isPlaying)
            {
                foreach (var pTex in _pooledTextures)
                {
                    DestroyImmediate(((RenderTexture)pTex.texture));
                }

                foreach (var kvp in _reflectionCameras)
                {
                    DestroyImmediate(((Camera)kvp.Value).gameObject);
                }
         
                DestroyImmediate(_clippingPlaneMeshLeft);
                DestroyImmediate(_clippingPlaneLeft);
                DestroyImmediate(_clippingPlaneMeshRight);
                DestroyImmediate(_clippingPlaneRight);
                DestroyImmediate(_clippingPlaneMaterial);                
            }
            else
            {
                foreach (var kvp in _reflectionCameras)
                {
                    Destroy(((Camera)kvp.Value).gameObject);
                }

                foreach (var pTex in _pooledTextures)
                {
                    Destroy(((RenderTexture)pTex.texture));
                }

                Destroy(_clippingPlaneMeshLeft);
                Destroy(_clippingPlaneLeft);
                Destroy(_clippingPlaneMeshRight);
                Destroy(_clippingPlaneRight);
                Destroy(_clippingPlaneMaterial);
            }

            _pooledTextures.Clear();
            _reflectionCameras.Clear();

            _UacPerCameras.Clear();
            _uacReflectionCam = null;
            _uacRenderCam = null;

            _clippingPlaneLeft = null;
            _clippingPlaneMeshLeft = null;
            _clippingPlaneRight = null;
            _clippingPlaneMeshRight = null;
            _clippingPlaneMaterial = null;

        }

        public static Portal FindClosestPortalInAllRenderers(Vector3 pos) 
        {
            Portal closestPortal = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < portalRendererInstances.Count; i++)
            {
                PortalRenderer pr = portalRendererInstances[i];
                for (int j = 0; j < pr.Portals.Count; j++)
                {
                    Portal _p = pr.Portals[j];
                    if (_p && _p.PortalSurface && _p.isActiveAndEnabled)
                    {
                        float myDistance = Vector3.Distance(pos, _p.PortalSurface.ClosestPointOnBounds(pos));
                        //Debug.Log("myDistance: " + myDistance);
                        if (myDistance < minDistance)
                        {
                            minDistance = myDistance;
                            closestPortal = _p;
                        }
                    }
                }
            }

            return closestPortal;
        }


#if UNITY_EDITOR
        public void SurfaceGotDeselectedInEditor()
        {
            // notify the other surfaces as the material might have changed, to update their materials
            if (Portals == null)
            {
                return;
            }



            foreach (Portal portal in Portals)
            {
                if (portal && portal.PortalSurface)
                {
                    portal.PortalSurface.RefreshMaterialInEditor();
                }
            }
        }
#endif
    }

    public class PooledPortalTexture
    {
        public bool liteLock;
        public bool fullLock;
        public CameraPortalMatrices matrices;
        public RenderTexture texture;
        //public int depth;
        public Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left;

        public PooledPortalTexture()
        {
        }
    }

    public class CameraPortalMatrices
    {
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 worldToCameraMatrix;
        public Matrix4x4 cullingMatrix;
        public PortalSurface mirrorSurface;
        public PortalSurface parentMirrorSurface;
        public PortalSurface parentsParentMirrorSurface;
        public PortalSurface parentsParentsParentMirrorSurface;
        public PortalSurface parentsParentsParentsParentMirrorSurface;
        public bool even;
        public Vector3 camPos;
        public int depth;
        public float distance;

        public CameraPortalMatrices(Matrix4x4 projectionMatrix, Matrix4x4 worldToCameraMatrix, Matrix4x4 cullingMatrix,
            PortalSurface mirrorSurface, 
            bool even,
            Vector3 camPos, int depth, float distance,
            PortalSurface parentMirrorSurface,
            PortalSurface parentsParentMirrorSurface,
            PortalSurface parentsParentsParentMirrorSurface,
            PortalSurface parentsParentsParentsParentMirrorSurface)
        {
            this.projectionMatrix = projectionMatrix;
            this.worldToCameraMatrix = worldToCameraMatrix;
            this.mirrorSurface = mirrorSurface;
            this.even = even;
            this.camPos = camPos;
            this.depth = depth;
            this.distance = distance;
            this.parentMirrorSurface = parentMirrorSurface;
            this.parentsParentMirrorSurface = parentsParentMirrorSurface;
            this.parentsParentsParentMirrorSurface = parentsParentsParentMirrorSurface;
            this.parentsParentsParentsParentMirrorSurface = parentsParentsParentsParentMirrorSurface;
            this.cullingMatrix = cullingMatrix;
        }
    }
}