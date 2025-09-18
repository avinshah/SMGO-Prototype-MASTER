using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;
using System;
using UnityEngine.Rendering.Universal;
//using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fragilem17.MirrorsAndPortals
{
    [ExecuteInEditMode]
    public class PortalSurface : MonoBehaviour
    {
        private Portal _portal;

        [Tooltip("The source material, disable and re-enable this component if you make changes to the material")]
        public Material Material;

        [Tooltip("When the camera is further from this distance, the surface stops updating it's texture.")]
        [MinAttribute(0)]
        public float maxRenderingDistance = 5f;

        [Tooltip("Defines whether a certain amount of color should be blended in with the reflection depending on distance.")]
        public bool useColorBlending = true;

        public Color BlendColor = Color.black;

        [Tooltip("The value at 'Time' 0, means when you're directly in front of the portal at a distance of 0. 'Time' 1 reflects the maxRenderingDistance.")]
        public AnimationCurve colorBlendingCurve = AnimationCurve.Linear(0, -99, 1, 99);
  

        [Space(10)]

        [Tooltip("Defines whether the albedo map alpha should be changed over distance.")]
        public bool useAlbedoAlphaFading = false;

        public AnimationCurve albedoAlphaFadeCurve = AnimationCurve.Linear(0, -99, 1, 99);


        [Space(10)]

        [Tooltip("Defines whether the amount of Refraction should be changed depending on distance.")]
        public bool useRefractionFading = false;

        [Tooltip("The value at 'Time' 0, means when you're directly in front of the portal at a distance of 0. 'Time' 1 reflects the maxRenderingDistance.")]
        public AnimationCurve refractionFadingCurve = AnimationCurve.Linear(0, -99, 1, 99);

        

        [Header("Other")]

        public float clippingPlaneOffset = -0.002f;
        private Plane _plane;


        public MeshRenderer myMeshRenderer;
        private Material _material;
        private PortalRenderer _myRenderer;
        private Color _oldFadeColor = Color.black;
        private Material _oldMaterial;
        private bool _wasToFar = false;

        private bool _isSelectedInEditor = false;

        [HideInInspector]
        public Texture _currentTexLeft;
        [HideInInspector]
        public Texture _currentTexRight;
        [HideInInspector]
        public float _currentDistanceBlend;

        private Vector3 _startPosition;

        public Portal Portal { get => _portal; }



        private void OnEnable()
        {
#if UNITY_EDITOR
            SetDefaultCurves();
#endif
            _startPosition = transform.localPosition;

            _portal = GetComponentInParent<Portal>();
            if (!_portal)
            {
                Debug.LogWarning(PortalUtils.Colorize("[PORTALS] ", PortalUtils.DebugColors.Warn, true) + name + " a PortalSurface needs to be a child gameObject of a Portal");
            }

            //Debug.Log("_isSelectedInEditor " + _isSelectedInEditor + " : "+ gameObject.name);
            if (myMeshRenderer == null)
            {
                myMeshRenderer = GetComponent<MeshRenderer>();
            }

            

            _wasToFar = false;

            if (!Material && myMeshRenderer)
            {
                Material = myMeshRenderer.sharedMaterial;
            }


            if (myMeshRenderer && Material)
            {
                _oldMaterial = Material;

                if (_isSelectedInEditor)
                {
                    // make sure we're editing the source materials, not the instance
                    Material.SetColor("_FadeColor", BlendColor);
                    myMeshRenderer.sharedMaterial = Material;
                    _material = Material;
                }
                else
                {
                    _material = new Material(Material);
                    _material.name += " (for " + gameObject.name + ")";
                    _material.SetColor("_FadeColor", BlendColor);
                    myMeshRenderer.material = _material;
                }
            }

            if (_material.HasProperty("_FadeColorBlend"))
            {
                _currentDistanceBlend = _material.GetFloat("_FadeColorBlend");
            }

#if UNITY_EDITOR
            Selection.selectionChanged += OnSelectionChange;
#endif          
        }


        private void OnDisable()
        {            

#if UNITY_EDITOR
            //Debug.Log("disabling");
            _isSelectedInEditor = false;
            Selection.selectionChanged -= OnSelectionChange;
#endif
            if (_material != Material)
            {
                DestroyImmediate(_material, true);
            }
            if (myMeshRenderer)
            {
                myMeshRenderer.material = Material;
            }
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            //Debug.Log("destroying");
            _isSelectedInEditor = false;
            Selection.selectionChanged -= OnSelectionChange;
            //DestroyImmediate(gameObject);
        }

        [ContextMenu("Copy my properties and settings to the other PortalSurface.")]
        void CopyPropertiesToOtherPortal()
        {
            if (_portal && _portal.OtherPortal && _portal.OtherPortal.PortalSurface)
            {
                _portal.OtherPortal.PortalSurface.Material = Material;
                _portal.OtherPortal.PortalSurface.maxRenderingDistance = maxRenderingDistance;
                _portal.OtherPortal.PortalSurface.useColorBlending = useColorBlending;
                _portal.OtherPortal.PortalSurface.BlendColor = BlendColor;
                _portal.OtherPortal.PortalSurface.colorBlendingCurve.keys = colorBlendingCurve.keys;
                _portal.OtherPortal.PortalSurface.useAlbedoAlphaFading = useAlbedoAlphaFading;
                _portal.OtherPortal.PortalSurface.albedoAlphaFadeCurve.keys = albedoAlphaFadeCurve.keys;
                _portal.OtherPortal.PortalSurface.useRefractionFading = useRefractionFading;
                _portal.OtherPortal.PortalSurface.refractionFadingCurve.keys = refractionFadingCurve.keys;
                _portal.OtherPortal.PortalSurface.clippingPlaneOffset = clippingPlaneOffset;
                EditorUtility.SetDirty(_portal.OtherPortal.PortalSurface);
            }
        }

        [ContextMenu("Serialise My Curves")]
        private void SerialiseMyCurves()
        {
            SerializableCurve sc = new SerializableCurve(colorBlendingCurve);
            string json = JsonUtility.ToJson(sc);
            //json = JsonConvert.ToString(json);
            Debug.Log("colorBlendingCurve: " + json);

            sc = new SerializableCurve(albedoAlphaFadeCurve);
            json = JsonUtility.ToJson(sc);
            //json = JsonConvert.ToString(json);
            Debug.Log("albedoAlphaFadeCurve: " + json);

            sc = new SerializableCurve(refractionFadingCurve);
            json = JsonUtility.ToJson(sc);
            //json = JsonConvert.ToString(json);
            Debug.Log("refractionFadingCurve: " + json);
        }

        private void SetDefaultCurves()
        {
            if (colorBlendingCurve.keys.Length == 2 && colorBlendingCurve.keys[0].value == -99 && colorBlendingCurve.keys[1].value == 99)
            {
                SetDefaultColorBlendingCurve();
                EditorUtility.SetDirty(this);
            }

            if (albedoAlphaFadeCurve.keys.Length == 2 && albedoAlphaFadeCurve.keys[0].value == -99 && albedoAlphaFadeCurve.keys[1].value == 99)
            {
                SetDefaultAlbedoAlphaFadeCurve();
                EditorUtility.SetDirty(this);
            }

            if (refractionFadingCurve.keys.Length == 2 && refractionFadingCurve.keys[0].value == -99 && refractionFadingCurve.keys[1].value == 99)
            {
                SetDefaultRefractionFadingCurve();
                EditorUtility.SetDirty(this);
            }
        }

        [ContextMenu("Reset RefractionFadingCurve")]
        private void SetDefaultRefractionFadingCurve()
        {
            string curve = "{\"keys\":[{\"inTangent\":0.0,\"inWeight\":0.0,\"outTangent\":-0.0003126605006400496,\"outWeight\":1.0,\"weightedMode\":0,\"time\":0.0,\"value\":0.0},{\"inTangent\":0.0,\"inWeight\":0.3333333432674408,\"outTangent\":0.0,\"outWeight\":0.3333333432674408,\"weightedMode\":0,\"time\":0.05000000074505806,\"value\":0.0},{\"inTangent\":-0.005003984551876783,\"inWeight\":0.12056756019592285,\"outTangent\":-0.005003984551876783,\"outWeight\":0.0,\"weightedMode\":0,\"time\":0.9946242570877075,\"value\":0.1989855170249939}],\"postWrapMode\":\"ClampForever\",\"preWrapMode\":\"ClampForever\"}";
            SerializableCurve sc2 = JsonUtility.FromJson<SerializableCurve>(curve);
            refractionFadingCurve = sc2.toCurve();
        }

        [ContextMenu("Reset AlbedoAlphaFadeCurve")]
        private void SetDefaultAlbedoAlphaFadeCurve()
        {
            string curve = "{\"keys\":[{\"inTangent\":0.0,\"inWeight\":0.0,\"outTangent\":0.0,\"outWeight\":0.0,\"weightedMode\":0,\"time\":0.0,\"value\":0.0},{\"inTangent\":0.0,\"inWeight\":1.0,\"outTangent\":45.0,\"outWeight\":0.022694604471325876,\"weightedMode\":0,\"time\":0.009999999776482582,\"value\":0.0},{\"inTangent\":45.0,\"inWeight\":0.8593189716339111,\"outTangent\":0.0,\"outWeight\":0.11139071732759476,\"weightedMode\":0,\"time\":0.029999999329447748,\"value\":0.8999999761581421},{\"inTangent\":0.03659231960773468,\"inWeight\":0.1438542902469635,\"outTangent\":0.03659231960773468,\"outWeight\":0.3333333432674408,\"weightedMode\":0,\"time\":0.6000000238418579,\"value\":0.8999999761581421},{\"inTangent\":0.0,\"inWeight\":0.0,\"outTangent\":0.0,\"outWeight\":0.0,\"weightedMode\":0,\"time\":1.0,\"value\":2.200000047683716}],\"postWrapMode\":\"ClampForever\",\"preWrapMode\":\"ClampForever\"}";
            SerializableCurve sc2 = JsonUtility.FromJson<SerializableCurve>(curve);
            albedoAlphaFadeCurve = sc2.toCurve();
        }

        [ContextMenu("Reset ColorBlendingCurve")]
        private void SetDefaultColorBlendingCurve()
        {
            string curve = "{\"keys\":[{\"inTangent\":0.0,\"inWeight\":0.0,\"outTangent\":0.0,\"outWeight\":0.0,\"weightedMode\":0,\"time\":0.0,\"value\":0.0},{\"inTangent\":0.0,\"inWeight\":0.3333333432674408,\"outTangent\":0.0,\"outWeight\":0.3333333432674408,\"weightedMode\":0,\"time\":0.6000000238418579,\"value\":0.0},{\"inTangent\":0.0,\"inWeight\":0.0,\"outTangent\":0.0,\"outWeight\":0.0,\"weightedMode\":0,\"time\":1.0,\"value\":1.0}],\"postWrapMode\":\"ClampForever\",\"preWrapMode\":\"ClampForever\"}";
            SerializableCurve sc2 = JsonUtility.FromJson<SerializableCurve>(curve);
            colorBlendingCurve = sc2.toCurve();
        }
#endif

        public void UpdatePositionsInMaterial(Vector3 position, Vector3 direction)
        {
            if (_material && _material.HasProperty("_WorldPos"))
            {
                _material.SetVector("_WorldPos", position);
                _material.SetVector("_WorldDir", direction);
            }
        }

        public bool VisibleFromCamera(Camera renderCamera, bool ignoreDistance = true, float offset = 0, bool ignoreCulling = false)
        {
            if (!enabled || !myMeshRenderer || !_material || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!ignoreCulling && !myMeshRenderer.isVisible)
            {
                //Debug.Log(name + " i'm not visible!");
                return false;
            }

            // check the normal of the mirror. if the camera is behind it, return early
            Vector3 forward = -1 * transform.forward; //transform.TransformDirection(Vector3.forward);
            //Vector3 toOther = (renderCamera.transform.position+(renderCamera.transform.forward * -offset)) - (transform.position);
            Vector3 toOther = (renderCamera.transform.position) - (transform.position);


            if (Vector3.Dot(forward, toOther) < -offset) // if we're behind the mirror 
            {
                /*if (!ignoreDistance)
                {
                    Debug.Log("behind! " + renderCamera.name + " : " + _portal.name + " dot: " + Vector3.Dot(forward, toOther) + " offset:" + offset);
                }*/
                /*
                if (!_wasToFar)
                {
                    _wasToFar = true;
                    
                    // blend the surface
                    if (useColorBlending && _material.HasProperty("_FadeColorBlend"))
                    {
                        Debug.Log("behind: " + gameObject.name);
                        _material.SetFloat("_FadeColorBlend", colorBlendingCurve.Evaluate(1));
                    }
                    if (useAlbedoAlphaFading && _material.HasProperty("_AlbedoAlpha"))
                    {
                        _material.SetFloat("_AlbedoAlpha", albedoAlphaFadeCurve.Evaluate(1));
                    }
                    if (useRefractionFading && _material.HasProperty("_refraction"))
                    {
                        _material.SetFloat("_refraction", refractionFadingCurve.Evaluate(1));
                    }
                    
            }
            */
                return false;
            }
            else
            {
                /*if (_wasToFar)
                {
                    _wasToFar = false;
                }*/
            }


            if (!ignoreDistance)
            {
                float d = Vector3.Distance(ClosestPointOnBounds(renderCamera.transform.position), renderCamera.transform.position);
                bool toFar = d > maxRenderingDistance;
                if (toFar && !_wasToFar)
                {
                    _wasToFar = true;

                    // blend the surface
                    if (useColorBlending && _material.HasProperty("_FadeColorBlend"))
                    {
                        //Debug.Log("toFar: " + gameObject.name + " distnce: "+ d);
                        _material.SetFloat("_FadeColorBlend", colorBlendingCurve.Evaluate(1));
                    }
                    if (useAlbedoAlphaFading && _material.HasProperty("_AlbedoAlpha"))
                    {
                        _material.SetFloat("_AlbedoAlpha", albedoAlphaFadeCurve.Evaluate(1));
                    }
                    if (useRefractionFading && _material.HasProperty("_refraction"))
                    {
                        _material.SetFloat("_refraction", refractionFadingCurve.Evaluate(1));
                    }
                }
                if (!toFar && _wasToFar)
                {
                    _wasToFar = false;
                }

                if (toFar)
                {
                    return false;
                }
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(renderCamera);
            planes[4].Translate(renderCamera.transform.forward * offset);          
            bool inBounds = GeometryUtility.TestPlanesAABB(planes, myMeshRenderer.bounds);


            /*if ((_portal.name == "DifferentScaledPortal1" || _portal.name == "DifferentScaledPortal2"))
            {
                Debug.Log("in front! " + _portal.name + " inBounds: " + inBounds + " dot: " + Vector3.Dot(forward, toOther) + " offset:" + offset);
                //Debug.Break();
            }*/

            return inBounds;
        }

        public bool ShouldRenderBasedOnDistance(Vector3 position)
        {
            if (!enabled)
            {
                return false;
            }

            if (!_material)
            {
                return false;
            }

            float d = Vector3.Distance(ClosestPointOnBounds(position), position);
            bool toFar = d > maxRenderingDistance;

            if (toFar && !_wasToFar)
            {
                _wasToFar = true;

                // blend the surface
                if (_material.HasProperty("_FadeColorBlend"))
                {
                    _material.SetFloat("_FadeColorBlend", 1);
                    //Debug.Log("_wasToFar: " + gameObject.name + " blend " + 1 + " distance: " + d);
                }
            }

            if (!toFar && _wasToFar)
            {
                _wasToFar = false;
            }

            return !toFar;
        }

        public Vector3 ClosestPointOnBounds(Vector3 toPos)
        {
            Vector3 p = myMeshRenderer.bounds.ClosestPoint(toPos);
            p = _plane.ClosestPointOnPlane(p);
            return p;
        }

        /*public Vector3 ClosestPointOnPlane(Vector3 toPos)
        {
            return _plane.ClosestPointOnPlane(toPos);
        }*/

        public void UpdateMaterial(Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left, RenderTexture texture = null, PortalRenderer myRenderer = null, int depth = 1, float distance = 0)
        {
            if (myMeshRenderer && _material)
            {
                //Debug.Log(gameObject.name + " set prop 3");

                float distPercent = distance / maxRenderingDistance;
                float dist = distance;
                _myRenderer = myRenderer;
                //Debug.Log(name + " : " + distance + " : " + distPercent);
                Material m = _material;

                if (depth >= _myRenderer.recursions + 1)
                {
                    // we need to be fully opaque.. no need to do anything else
                    if (useColorBlending && m.HasProperty("_FadeColorBlend"))
                    {
                        m.SetFloat("_FadeColorBlend", colorBlendingCurve.Evaluate(1));
                    }
                    if (useAlbedoAlphaFading && m.HasProperty("_AlbedoAlpha"))
                    {
                        m.SetFloat("_AlbedoAlpha", albedoAlphaFadeCurve.Evaluate(1));
                    }
                    if (useRefractionFading && m.HasProperty("_refraction"))
                    {
                        m.SetFloat("_refraction", refractionFadingCurve.Evaluate(1));
                    }
                    return;
                }


                if (m.HasProperty("_ForceEye"))
                {
                    m.SetInt("_ForceEye", eye == Camera.StereoscopicEye.Left ? 0 : 1);
                }

                if (eye == Camera.StereoscopicEye.Left && m.HasProperty("_TexLeft") && texture != null)
                {
                    m.SetTexture("_TexLeft", texture);
                    _currentTexLeft = texture;
                }

                if (eye == Camera.StereoscopicEye.Right && XRSettings.enabled && m.HasProperty("_TexRight") && texture != null)
                {
                    m.SetTexture("_TexRight", texture);
                    _currentTexRight = texture;
                }

                EnableTransparancy();


                if (depth != -1)
                {
                    if (useColorBlending)
                    {
                        if (m.HasProperty("_FadeColorBlend"))
                        {
                            float curveVal = colorBlendingCurve.Evaluate(distPercent);
                            m.SetFloat("_FadeColorBlend", curveVal);
                            //Debug.Log("from surface: " + transform.parent.name + " : " + curveVal);
                            _currentDistanceBlend = curveVal;
                            //Debug.Log(gameObject.name + " 2 blend " + curveVal + " distance: " + distance);
                        }
                    }

                    if (useRefractionFading)
                    {
                        if (m.HasProperty("_refraction"))
                        {
                            float curveVal = refractionFadingCurve.Evaluate(distPercent);
                            m.SetFloat("_refraction", curveVal);
                            //Debug.Log(gameObject.name + " 2 blend " + blend + " distance: " + distance);
                        }                        
                    }

                    if (useAlbedoAlphaFading)
                    {
                        if (m.HasProperty("_AlbedoAlpha"))
                        {
                            float curveVal = albedoAlphaFadeCurve.Evaluate(distPercent);
                            m.SetFloat("_AlbedoAlpha", curveVal);
                            //Debug.Log(gameObject.name + " 2 blend " + blend + " distance: " + distance);
                        }                       
                    }
                }
            }
        }




#if UNITY_EDITOR
        void OnSelectionChange()
        {
            if (this && isActiveAndEnabled)
            {
                if (gameObject == Selection.activeGameObject)
                {
                    _isSelectedInEditor = true;

                    // make sure we're editing the source materials, not the instance
                    if (Material != null)
                    {
                        Material.SetColor("_FadeColor", BlendColor);
                        myMeshRenderer.sharedMaterial = Material;
                        _material = Material;
                    }
                }
                else if (_isSelectedInEditor)
                {
                    // i'm no longer selected
                    _isSelectedInEditor = false;

                    OnDisable();
                    OnEnable();

                    if (_myRenderer != null)
                    {
                        _myRenderer.SurfaceGotDeselectedInEditor();
                    }
                }
            }
        }

        public void RefreshMaterialInEditor()
        {
            OnDisable();
            OnEnable();
        }

        private void Update()
        {
            _plane = new Plane(-transform.forward, transform.position);

            //if (_oldFadeColor != (FadeColor.r + FadeColor.g + FadeColor.b))
            if (!BlendColor.Equals(_oldFadeColor))
            {
                if (_material)
                {
                    //Debug.Log(gameObject.name + " set prop 1");
                    _material.SetColor("_FadeColor", BlendColor);
                }
                //_oldFadeColor = (FadeColor.r + FadeColor.g + FadeColor.b);
                _oldFadeColor = BlendColor;
            }

            if (_oldMaterial != Material)
            {
                _material = Material;
                RefreshMaterialInEditor();
            }
        }

#endif

#if !UNITY_EDITOR 
        private void Update()
        {
            _plane = new Plane(-transform.forward, transform.position);
        }
#endif

        public void EnableTransparancy()
        {
            if (_material)
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_useTransparency", 1);
            }
        }

        public void DisableTransparancy()
        {
            if (_material)
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_useTransparency", 0);
            }
        }

        public void TurnOffForceEye()
        {
            if (_material && _material.HasProperty("_ForceEye"))
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_ForceEye", -1);
            }
        }
        public void ForceLeftEye()
        {
            if (_material && _material.HasProperty("_ForceEye"))
            {
                //Debug.Log(gameObject.name + " set prop 2");
                _material.SetInt("_ForceEye", 0);
            }
        }
    }

    public class MinToAttribute : PropertyAttribute
    {
        public float? max;
        public float min;

        public MinToAttribute() { }
        public MinToAttribute(float max, float min = 0)
        {
            this.max = max;
            this.min = min;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinToAttribute))]
    public class MinToDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            var ctrlRect = EditorGUI.PrefixLabel(position, label);
            Rect[] r = SplitRectIn3(ctrlRect, 36, 5);
            var att = (MinToAttribute)attribute;
            var type = property.propertyType;
            if (type == SerializedPropertyType.Vector2)
            {
                var vec = property.vector2Value;
                float min = vec.x;
                float to = vec.y;
                min = EditorGUI.FloatField(r[0], min);
                to = EditorGUI.FloatField(r[2], to);
                EditorGUI.MinMaxSlider(r[1], ref min, ref to, att.min, att.max ?? to);
                vec = new Vector2(min < to ? min : to, to);
                property.vector2Value = vec;
            }
            else if (type == SerializedPropertyType.Vector2Int)
            {
                var vec = property.vector2IntValue;
                float min = vec.x;
                float to = vec.y;
                min = EditorGUI.IntField(r[0], (int)min);
                to = EditorGUI.IntField(r[2], (int)to);
                EditorGUI.MinMaxSlider(r[1], ref min, ref to, att.min, att.max ?? to);
                vec = new Vector2Int(Mathf.RoundToInt(min < to ? min : to), Mathf.RoundToInt(to));
                property.vector2IntValue = vec;
            }
            else
                EditorGUI.HelpBox(ctrlRect, "MinTo is for Vector2!!", MessageType.Error);
        }

        public static Rect[] SplitRectIn3(Rect rect, int bordersSize, int space = 0)
        {
            var r = SplitRect(rect, 3);
            int pad = (int)r[0].width - bordersSize;
            int ps = pad + space;
            r[0].width = r[2].width -= ps;
            r[1].width += pad * 2;
            r[1].x -= pad;
            r[2].x += ps;
            return r;
        }
        public static Rect[] SplitRect(Rect a, int n)
        {
            Rect[] r = new Rect[n];
            for (int i = 0; i < n; ++i)
                r[i] = new Rect(a.x + a.width / n * i, a.y, a.width / n, a.height);
            return r;
        }
    }
#endif
}
