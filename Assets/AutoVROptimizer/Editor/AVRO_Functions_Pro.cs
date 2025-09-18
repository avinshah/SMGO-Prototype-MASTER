/*
Copyright (c) 2025 Valem Studio

This asset is the intellectual property of Valem Studio and is distributed under the Unity Asset Store End User License Agreement (EULA).

Unauthorized reproduction, modification, or redistribution of any part of this asset outside the terms of the Unity Asset Store EULA is strictly prohibited.

For support or inquiries, please contact Valem Studio via social media or through the publisher profile on the Unity Asset Store.
*/

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
namespace AVRO
{
    public static class AVRO_Functions_Pro
    {
        #region Project Settings
        //----------------------------------------
        //  PROJECT SETTINGS
        //----------------------------------------
        // Project 05 - OculusXR Or OpenXR
        // NO FIX
        public static AVRO_Settings.TicketStates GetOculusXROrOpenXR(AVRO_Ticket _ticket)
        {
            // If Meta package > 74 ?
            return AVRO_Utilities.IsOpenXRInstalled != AVRO_Utilities.IsOculusXRInstalled ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        #endregion

        #region Player Settings
        //----------------------------------------
        //  PLAYER SETTINGS
        //----------------------------------------
        // Player 05 - GraphicsJobsLegacy
        public static AVRO_Settings.TicketStates GetGraphicsJobsModeLegacy(AVRO_Ticket _ticket)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs == null || objs.Length == 0)
                return AVRO_Settings.TicketStates.Omitted;
            SerializedObject settingsAsset = new SerializedObject(objs[0]);
            SerializedProperty jobModeArray = settingsAsset.FindProperty("m_BuildTargetGraphicsJobMode");
            if (jobModeArray == null || !jobModeArray.isArray)
                return AVRO_Settings.TicketStates.Omitted;
            for (int i = 0; i < jobModeArray.arraySize; i++)
            {
                SerializedProperty element = jobModeArray.GetArrayElementAtIndex(i);
                SerializedProperty buildTarget = element.FindPropertyRelative("m_BuildTarget");
                SerializedProperty jobMode = element.FindPropertyRelative("m_GraphicsJobMode");
                if (buildTarget != null && buildTarget.stringValue == "AndroidPlayer")
                {
                    if (jobMode != null)
                        return jobMode.intValue == 1 ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
                }
            }
            return AVRO_Settings.TicketStates.Omitted;
        }
        public static void SetGraphicsJobsModeLegacy(AVRO_Ticket _ticket)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs == null || objs.Length == 0) return;
            SerializedObject settingsAsset = new SerializedObject(objs[0]);
            SerializedProperty jobModeArray = settingsAsset.FindProperty("m_BuildTargetGraphicsJobMode");
            if (jobModeArray == null || !jobModeArray.isArray) return;
            for (int i = 0; i < jobModeArray.arraySize; i++)
            {
                SerializedProperty element = jobModeArray.GetArrayElementAtIndex(i);
                SerializedProperty buildTarget = element.FindPropertyRelative("m_BuildTarget");
                SerializedProperty jobMode = element.FindPropertyRelative("m_GraphicsJobMode");
                if (buildTarget != null && buildTarget.stringValue == "AndroidPlayer")
                {
                    if (jobMode != null)
                    {
                        jobMode.intValue = 1;
                        settingsAsset.ApplyModifiedProperties();
                    }
                }
            }
        }

        // Player 06 - Color Gammut sRGB
        public static AVRO_Settings.TicketStates GetColorGamut_sRGB(AVRO_Ticket _ticket)
        {
            // TODO
            return AVRO_Settings.TicketStates.Omitted;
        }
        public static void SetColorGamut_sRGB(AVRO_Ticket _ticket)
        {
            // TODO
        }

        // Player 09 - Lightmap Streaming
        public static AVRO_Settings.TicketStates GetLightmapStreamingEnabled(AVRO_Ticket _ticket)
        {
#if UNITY_6000_1_OR_NEWER
            string _path = "ProjectSettings/ProjectSettings.asset";
            if (!File.Exists(_path))
                return AVRO_Settings.TicketStates.Omitted;
            string _content = File.ReadAllText(_path);
            if (!_content.Contains("m_TextureStreamingEnabled"))
                return AVRO_Settings.TicketStates.Omitted;
            return Regex.IsMatch(_content, @"m_TextureStreamingEnabled:\s*1") ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
#else
            var _objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (_objs == null || _objs.Length == 0)
                return AVRO_Settings.TicketStates.Omitted;
            SerializedObject _settingsAsset = new SerializedObject(_objs[0]);
            SerializedProperty _arrayProp = _settingsAsset.FindProperty("m_BuildTargetGroupLightmapSettings");
            if (_arrayProp == null || !_arrayProp.isArray)
                return AVRO_Settings.TicketStates.Omitted;
            for (int i = 0; i < _arrayProp.arraySize; i++)
            {
                SerializedProperty _element = _arrayProp.GetArrayElementAtIndex(i);
                SerializedProperty _buildTargetProp = _element.FindPropertyRelative("m_BuildTarget");

                if (_buildTargetProp != null && _buildTargetProp.stringValue == "Android")
                {
                    SerializedProperty _streamingEnabled = _element.FindPropertyRelative("m_TextureStreamingEnabled");
                    if (_streamingEnabled != null)
                        return _streamingEnabled.boolValue == true ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
                }
            }
            return AVRO_Settings.TicketStates.Omitted;
#endif
        }
        public static void SetLightmapStreamingEnabled(AVRO_Ticket _ticket)
        {
#if UNITY_6000_1_OR_NEWER
            string _path = "ProjectSettings/ProjectSettings.asset";
            if (!File.Exists(_path)) return;
            string _content = File.ReadAllText(_path);
            string _newContent = Regex.Replace(_content, @"(m_TextureStreamingEnabled:\s*)0", "${1}1");
            File.WriteAllText(_path, _newContent);
            AssetDatabase.Refresh();
#else
            var _objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (_objs == null || _objs.Length == 0) return;
            SerializedObject _settingsAsset = new SerializedObject(_objs[0]);
            SerializedProperty _arrayProp = _settingsAsset.FindProperty("m_BuildTargetGroupLightmapSettings");
            if (_arrayProp == null || !_arrayProp.isArray) return;
            for (int i = 0; i < _arrayProp.arraySize; i++)
            {
                SerializedProperty _element = _arrayProp.GetArrayElementAtIndex(i);
                SerializedProperty _buildTargetProp = _element.FindPropertyRelative("m_BuildTarget");

                if (_buildTargetProp != null && _buildTargetProp.stringValue == "Android")
                {
                    SerializedProperty _streamingEnabled = _element.FindPropertyRelative("m_TextureStreamingEnabled");
                    if (_streamingEnabled != null)
                    {
                        _streamingEnabled.boolValue = true;
                        _settingsAsset.ApplyModifiedProperties();
                    }
                }
            }
#endif
        }

        // Player 10 - Disable HDRP Display Output
        public static AVRO_Settings.TicketStates GetHDRPDisplayOutputDisabled(AVRO_Ticket _ticket)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs == null || objs.Length == 0)
                return AVRO_Settings.TicketStates.Omitted;
            SerializedObject settingsAsset = new SerializedObject(objs[0]);
            SerializedProperty hdrSupport = settingsAsset.FindProperty("allowHDRDisplaySupport");
            if (hdrSupport != null && hdrSupport.propertyType == SerializedPropertyType.Boolean)
                return hdrSupport.boolValue == false ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
            return AVRO_Settings.TicketStates.Omitted;
        }
        public static void SetHDRPDisplayOutputDisabled(AVRO_Ticket _ticket)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs == null || objs.Length == 0) return;
            SerializedObject settingsAsset = new SerializedObject(objs[0]);
            SerializedProperty hdrSupport = settingsAsset.FindProperty("allowHDRDisplaySupport");
            if (hdrSupport != null && hdrSupport.propertyType == SerializedPropertyType.Boolean)
            {
                hdrSupport.boolValue = false;
                settingsAsset.ApplyModifiedProperties();
            }
        }

        // Player 18 - 32Bits Display Buffer
        public static AVRO_Settings.TicketStates GetDisplayBuffer(AVRO_Ticket _ticket)
        {
            return PlayerSettings.use32BitDisplayBuffer ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetDisplayBuffer(AVRO_Ticket _ticket)
        {
            PlayerSettings.use32BitDisplayBuffer = true;
        }

        // Player 19 - Target Architecture arm64
        public static AVRO_Settings.TicketStates GetTargetArchitecture(AVRO_Ticket _ticket)
        {
            return PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64 ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetTargetArchitecture(AVRO_Ticket _ticket)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }
        #endregion

        #region Renderer Settings
        //----------------------------------------
        //  RENDERER SETTINGS (URP-Focused)
        //----------------------------------------
        // Renderer 11 - One Max Light
        public static AVRO_Settings.TicketStates GetOnlyOneRealtimeLight(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            // TODO
            return AVRO_Settings.TicketStates.Todo;
        }
        public static void SetOnlyOneRealtimeLight(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected) return;
            // TODO
        }

        // Renderer 16 - Disable SSAO
        // NO FIX - READ ONLY
        public static AVRO_Settings.TicketStates GetDisableSSAO(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            var _rendererListField = AVRO_Utilities.URPSettings.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var _renderers = _rendererListField?.GetValue(AVRO_Utilities.URPSettings) as object[];
            if (_renderers == null || _renderers.Length == 0 || _renderers[0] == null)
                return AVRO_Settings.TicketStates.Omitted;
            var _urd = _renderers[0];
            var _rendererFeaturesField = _urd.GetType().GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            var _features = _rendererFeaturesField?.GetValue(_urd) as IList;
            if (_features == null)
                return AVRO_Settings.TicketStates.Omitted;
            foreach (var feature in _features)
            {
                var _type = feature.GetType();
                if (_type.Name == "ScreenSpaceAmbientOcclusion")
                {
                    var _isActiveProp = _type.GetProperty("isActive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_isActiveProp != null && _isActiveProp.GetValue(feature) is bool isActive && isActive)
                        return AVRO_Settings.TicketStates.Todo;

                    var _isActiveField = _type.GetField("isActive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_isActiveField != null && _isActiveField.GetValue(feature) is bool isActiveFieldVal && isActiveFieldVal)
                        return AVRO_Settings.TicketStates.Todo;
                }
            }
            return AVRO_Settings.TicketStates.Done;
        }

        // Renderer 17 - Intermediate Texture Auto
        public static AVRO_Settings.TicketStates GetIntermediateTextureAuto(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            var _rendererListField = AVRO_Utilities.URPSettings.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var _renderers = _rendererListField?.GetValue(AVRO_Utilities.URPSettings) as object[];
            if (_renderers == null || _renderers.Length == 0 || _renderers[0] == null)
                return AVRO_Settings.TicketStates.Omitted;
            var _urd = _renderers[0];
            var _m_IntermediateTextureMode = _urd.GetType().GetField("m_IntermediateTextureMode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_m_IntermediateTextureMode == null)
                return AVRO_Settings.TicketStates.Omitted;
            return (int)_m_IntermediateTextureMode.GetValue(_urd) == 0 ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetIntermediateTextureAuto(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return;
            var m_RendererDataList = AVRO_Utilities.URPSettings.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var _renderers = m_RendererDataList?.GetValue(AVRO_Utilities.URPSettings) as object[];
            if (_renderers == null || _renderers.Length == 0 || _renderers[0] == null)
                return;
            var _urd = _renderers[0];
            var m_IntermediateTextureMode = _urd.GetType().GetField("m_IntermediateTextureMode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_IntermediateTextureMode != null)
            {
                m_IntermediateTextureMode.SetValue(_urd, 0);
                EditorUtility.SetDirty((UnityEngine.Object)AVRO_Utilities.URPSettings);
            }
        }

        #region.  Unity2023+
#if UNITY_2023_1_OR_NEWER// && IS_RENDERER_SELECTED
        // Renderer 04 - GPUResidentDrawer (experimental)
        public static AVRO_Settings.TicketStates GetGPUResidentDrawerEnabled(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_GPUResidentDrawerMode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field == null)
                return AVRO_Settings.TicketStates.Omitted;
            return _field.GetValue(AVRO_Utilities.URPSettings).ToString() == "InstancedDrawing" ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetGPUResidentDrawerEnabled(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_GPUResidentDrawerMode", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field != null)
            {
                var enumValue = Enum.ToObject(_field.FieldType, 1);
                _field.SetValue(AVRO_Utilities.URPSettings, enumValue);
                EditorUtility.SetDirty((UnityEngine.Object)AVRO_Utilities.URPSettings);
            }
        }

        // Renderer 05 - GPU Occlusion Culling
        public static AVRO_Settings.TicketStates GetGPUOcclusionCullingEnabled(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_GPUResidentDrawerEnableOcclusionCullingInCameras", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field == null)
                return AVRO_Settings.TicketStates.Omitted;
            return (bool)_field.GetValue(AVRO_Utilities.URPSettings) ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetGPUOcclusionCullingEnabled(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_GPUResidentDrawerEnableOcclusionCullingInCameras", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field != null)
            {
                _field.SetValue(AVRO_Utilities.URPSettings, true);
                EditorUtility.SetDirty((UnityEngine.Object)AVRO_Utilities.URPSettings);
            }
        }

        // Renderer 09 - Upscaling Filter Fidelity FX
        public static AVRO_Settings.TicketStates GetUpscalingFilterFidelity(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return AVRO_Settings.TicketStates.Omitted;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_UpscalingFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field == null)
                return AVRO_Settings.TicketStates.Omitted;
            return (int)_field.GetValue(AVRO_Utilities.URPSettings) == 0 ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetUpscalingFilterFidelity(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP || !AVRO_Utilities.IsRendererSelected)
                return;
            var _field = AVRO_Utilities.URPSettings.GetType().GetField("m_UpscalingFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_field != null)
            {
                _field.SetValue(AVRO_Utilities.URPSettings, 0);
                EditorUtility.SetDirty((UnityEngine.Object)AVRO_Utilities.URPSettings);
            }
        }
#endif
        #endregion

        #endregion

        #region Scene Settings
        //----------------------------------------
        //  SCENE SETTINGS
        //----------------------------------------
        // Scene 1  - No Volume in Scene
#if UNITY_2023_1_OR_NEWER
        public static AVRO_Settings.TicketStates GetNoVolumeInScene(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            foreach (Volume _volume in Volume.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_volume.gameObject);
                _ticket.AddObjectGUID(_id.ToString());
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetNoVolumeInScene(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    _obj.SetActive(false);
                }
            }
        }
#endif

        // Scene 02 - Multiple Materials Per Object
        // NO FIX
        public static AVRO_Settings.TicketStates GetMultipleMaterials(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (MeshRenderer _mr in MeshRenderer.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
#else
            foreach (MeshRenderer _mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
#endif
            {
                if (_mr.sharedMaterials.Length > 1)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_mr.gameObject);
                    _ticket.AddObjectGUID(_id.ToString(), _mr.sharedMaterials.Length);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 03 - OVR Use Recommend MSAA Level
        public static AVRO_Settings.TicketStates GetOVRMSAALevel(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id))
                return AVRO_Settings.TicketStates.Omitted;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            FieldInfo _useRecommendedMSAALevel = _OVRObject.GetType().GetField("useRecommendedMSAALevel");
            if (_useRecommendedMSAALevel == null)
                return AVRO_Settings.TicketStates.Omitted;
            return (bool)_useRecommendedMSAALevel.GetValue(_OVRObject) ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetOVRMSAALevel(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id)) return;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            FieldInfo _useRecommendedMSAALevel = _OVRObject.GetType().GetField("useRecommendedMSAALevel");
            _useRecommendedMSAALevel.SetValue(_OVRObject, true);
            AVRO_Utilities.SetDirtyReflectionOf(_OVRObject);
        }

        // Scene 04 - OVR Sharpen Type
        public static AVRO_Settings.TicketStates GetOVRSharpenType(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id))
                return AVRO_Settings.TicketStates.Omitted;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            PropertyInfo _sharpenType = _OVRObject.GetType().GetProperty("sharpenType");
            if (_sharpenType == null)
                return AVRO_Settings.TicketStates.Omitted;
            object _stereoRenderingModeValue = _sharpenType.GetValue(_OVRObject);
            Type _stereoRenderingModeEnumType = _sharpenType.PropertyType;
            object _multiviewEnumValue = Enum.Parse(_stereoRenderingModeEnumType, "None");
            return _stereoRenderingModeValue.Equals(_multiviewEnumValue) ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetOVRSharpenType(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id)) return;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            PropertyInfo _sharpenType = _OVRObject.GetType().GetProperty("sharpenType");
            if (_sharpenType == null) return;
            Type _enumType = _sharpenType.PropertyType;
            object _multiviewValue = Enum.Parse(_enumType, "None"); // Normal - Quality - Automatic
            _sharpenType.SetValue(_OVRObject, _multiviewValue);

            AVRO_Utilities.SetDirtyReflectionOf(_OVRObject);
        }

        // Scene 05 - OVR Processor Favor
        // NO FIX
        public static AVRO_Settings.TicketStates GetOVRProcessorFavor(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsMetaInstalled)
                return AVRO_Settings.TicketStates.Omitted;
            // Just informing the user to check the AVRO_Utilities.Settings
            return AVRO_Settings.TicketStates.Todo;
        }

        // Scene 06 - OVR Dynamic Resolution
        public static AVRO_Settings.TicketStates GetOVRDynamicResolution(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id))
                return AVRO_Settings.TicketStates.Omitted;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            PropertyInfo _enableDynamicResolution = _OVRObject.GetType().GetProperty("enableDynamicResolution");
            if (_enableDynamicResolution == null)
                return AVRO_Settings.TicketStates.Omitted;
            return (bool)_enableDynamicResolution.GetValue(_OVRObject) ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetOVRDynamicResolution(AVRO_Ticket _ticket)
        {
            GlobalObjectId _id;
            if (!AVRO_Utilities.IsMetaInstalled || AVRO_Utilities.OVRManagerGUID == null || !GlobalObjectId.TryParse(AVRO_Utilities.OVRManagerGUID, out _id)) return;
            var _OVRObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_id);
            PropertyInfo _enableDynamicResolution = _OVRObject.GetType().GetProperty("enableDynamicResolution");
            _enableDynamicResolution.SetValue(_OVRObject, true);
            AVRO_Utilities.SetDirtyReflectionOf(_OVRObject);
        }

        // Scene 07 - Triangles Count
        // NO FIX
        public static AVRO_Settings.TicketStates GetTrianglesCount(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            int _triangleCount = 0;
#if UNITY_2021_1_OR_NEWER
            foreach (MeshFilter _mf in MeshFilter.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
#else
            foreach (MeshFilter _mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
#endif
            {
                if (!_mf.sharedMesh)
                    continue;
                int _count = _mf.sharedMesh.triangles.Length / 3;
                var _lodGroup = _mf.GetComponentInParent<LODGroup>();
                if (_lodGroup)
                {
                    bool _isPartOfLOD0 = false;
                    foreach (var _lodR in _lodGroup.GetLODs()[0].renderers)
                    {
                        if (_mf.GetComponent<MeshRenderer>() == _lodR)
                        {
                            _isPartOfLOD0 = true;
                            break;
                        }
                    }
                    if (!_isPartOfLOD0)
                        _count = 0;
                }
                _triangleCount += _count;
            }
            _ticket.data.ticketValue = _triangleCount;
            return _triangleCount > AVRO_Utilities.Settings.TargetSceneTriangles ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 08 - Triangles Count Per Object
        // NO FIX
        public static AVRO_Settings.TicketStates GetTrianglesCountPerObject(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (MeshFilter _mf in MeshFilter.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
#else
            foreach (MeshFilter _mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
#endif
            {
                if (!_mf.sharedMesh)
                    continue;
                int _count = _mf.sharedMesh.triangles.Length / 3;
                if (_count > AVRO_Utilities.Settings.DisplayObjectsOfTrianglesOver)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_mf.gameObject);
                    _ticket.AddObjectGUID(_id.ToString(), _count);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 09 - Negative Scales
        public static AVRO_Settings.TicketStates GetNegativeScales(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (Transform _t in Transform.FindObjectsByType<Transform>(FindObjectsSortMode.None))
#else
            foreach (Transform _t in UnityEngine.Object.FindObjectsOfType<Transform>())
#endif
            {
                if (_t.lossyScale.x < 0 || _t.lossyScale.y < 0 || _t.lossyScale.z < 0)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_t.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetNegativeScales(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    Transform _t = _obj.transform;
                    Vector3 localScale = _t.localScale;
                    _t.localScale = new Vector3(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z));
                }
            }
        }

        // Scene 10 - Realtime Lights
        public static AVRO_Settings.TicketStates GetRealtimeLights(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (Light _light in Light.FindObjectsByType<Light>(FindObjectsSortMode.None))
#else
            foreach (Light _light in UnityEngine.Object.FindObjectsOfType<Light>())
#endif
            {
                if (_light.lightmapBakeType == LightmapBakeType.Realtime)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_light.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetRealtimeLights(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    Light _light = _obj.GetComponent<Light>();
                    _light.lightmapBakeType = LightmapBakeType.Baked;
                    _obj.isStatic = true;
                    EditorUtility.SetDirty(_light);
                }
            }
        }

        // Scene 11 - Realtime Reflection Probe
        public static AVRO_Settings.TicketStates GetRealtimeReflectionProbes(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (ReflectionProbe _rp in ReflectionProbe.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
#else
            foreach (ReflectionProbe _rp in UnityEngine.Object.FindObjectsOfType<ReflectionProbe>())
#endif
            {
                if (_rp.mode == ReflectionProbeMode.Realtime)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_rp.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetRealtimeReflectionProbes(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    ReflectionProbe _rp = _obj.GetComponent<ReflectionProbe>();
                    _rp.mode = ReflectionProbeMode.Baked;
                    _obj.isStatic = true;
                    EditorUtility.SetDirty(_rp);
                }
            }
        }

        // Scene 12 - Static Baked Lights
        public static AVRO_Settings.TicketStates GetStaticBakedLights(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (Light _light in Light.FindObjectsByType<Light>(FindObjectsSortMode.None))
#else
            foreach (Light _light in UnityEngine.Object.FindObjectsOfType<Light>())
#endif
            {
                if (_light.lightmapBakeType == LightmapBakeType.Baked && !_light.gameObject.isStatic)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_light.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetStaticBakedLights(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    _obj.isStatic = true;
                    EditorUtility.SetDirty(_obj);
                }
            }
        }

        // Scene 13 - Static Baked Reflection Probe
        public static AVRO_Settings.TicketStates GetStaticBakedReflectionProbe(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            foreach (ReflectionProbe _rp in ReflectionProbe.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
#else
            foreach (ReflectionProbe _rp in UnityEngine.Object.FindObjectsOfType<ReflectionProbe>())
#endif
            {
                if (_rp.mode == ReflectionProbeMode.Baked && !_rp.gameObject.isStatic)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_rp.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetStaticBakedReflectionProbe(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                if (GlobalObjectId.TryParse(_data.guid, out GlobalObjectId globalId))
                {
                    GameObject _obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                    _obj.isStatic = true;
                    EditorUtility.SetDirty(_obj);
                }
            }
        }

        // Scene 14 - Avoid Camera Stack
        public static AVRO_Settings.TicketStates GetAvoidCameraStack(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP)
                return AVRO_Settings.TicketStates.Omitted;
            _ticket.data.concernedObjects.Clear();
#if UNITY_2021_1_OR_NEWER
            var _cameras = Camera.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
            var _cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
#endif
            var _cameraDataType = GetUniversalAdditionalCameraDataType();
            if (_cameraDataType == null)
                return AVRO_Settings.TicketStates.Omitted;
            foreach (var _camera in _cameras)
            {
                var _cameraDataComponent = _camera.GetComponent(_cameraDataType);
                if (_cameraDataComponent != null)
                {
                    var _renderTypeProperty = _cameraDataType.GetProperty("renderType");
                    var _cameraStackProperty = _cameraDataType.GetProperty("cameraStack");
                    if (_renderTypeProperty != null && _cameraStackProperty != null)
                    {
                        var _renderTypeValue = _renderTypeProperty.GetValue(_cameraDataComponent);
                        var _cameraStackValue = _cameraStackProperty.GetValue(_cameraDataComponent) as IList;
                        if (_renderTypeValue != null && _cameraStackValue != null && _renderTypeValue.ToString() == "Base" && _cameraStackValue.Count > 0)
                        {
                            GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_camera.gameObject);
                            _ticket.AddObjectGUID(_id.ToString(), _cameraStackValue.Count);
                        }
                    }
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        private static Type GetUniversalAdditionalCameraDataType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var _type = assembly.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                if (_type != null)
                    return _type;
            }
            return null;
        }
        public static void SetAvoidCameraStack(AVRO_Ticket _ticket)
        {
            if (AVRO_Utilities.URPSettings == null) return;
#if UNITY_2021_1_OR_NEWER
            var _cameras = Camera.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
            var _cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
#endif
            var _cameraDataType = GetUniversalAdditionalCameraDataType();
            if (_cameraDataType == null) return;
            foreach (var _camera in _cameras)
            {
                var _cameraDataComponent = _camera.GetComponent(_cameraDataType);
                if (_cameraDataComponent != null)
                {
                    var _cameraStackProperty = _cameraDataType.GetProperty("cameraStack");
                    if (_cameraStackProperty != null)
                    {
                        var _cameraStackValue = _cameraStackProperty.GetValue(_cameraDataComponent) as IList;
                        if (_cameraStackValue != null && _cameraStackValue.Count > 0)
                        {
                            _cameraStackValue.Clear();
                            EditorUtility.SetDirty(_cameraDataComponent);
                        }
                    }
                }
            }
        }

        // Scene 15 - One Active Camera
        // NO FIX
        public static AVRO_Settings.TicketStates GetOneActiveCamera(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            List<Camera> _cameras = new List<Camera>();
#if UNITY_2021_1_OR_NEWER
            foreach (Camera _camera in Camera.FindObjectsByType<Camera>(FindObjectsSortMode.None))
#else
            foreach (Camera _camera in UnityEngine.Object.FindObjectsOfType<Camera>())
#endif
                if (_camera.gameObject.activeInHierarchy && _camera.enabled)
                    _cameras.Add(_camera);
            if (_cameras.Count > 1)
            {
                foreach (var _cam in _cameras)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_cam.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 16 - One Active Audio Listener
        // NO FIX
        public static AVRO_Settings.TicketStates GetOneActiveAudioListener(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            List<AudioListener> _ALs = new List<AudioListener>();
#if UNITY_2021_1_OR_NEWER
            foreach (AudioListener _AL in AudioListener.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
#else
            foreach (AudioListener _AL in UnityEngine.Object.FindObjectsOfType<AudioListener>())
#endif
                if (_AL.gameObject.activeInHierarchy && _AL.enabled)
                    _ALs.Add(_AL);
            if (_ALs.Count > 1)
            {
                foreach (var _al in _ALs)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_al.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 17 - One Main Camera
        // NO FIX
        public static AVRO_Settings.TicketStates GetOneMainCamera(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            List<Camera> _cameras = new List<Camera>();
#if UNITY_2021_1_OR_NEWER
            foreach (Camera _camera in Camera.FindObjectsByType<Camera>(FindObjectsSortMode.None))
#else
            foreach (Camera _camera in UnityEngine.Object.FindObjectsOfType<Camera>())
#endif
            {
                if (_camera.CompareTag("MainCamera"))
                    _cameras.Add(_camera);
            }
            if (_cameras.Count > 1)
            {
                foreach (var _cam in _cameras)
                {
                    GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(_cam.gameObject);
                    _ticket.AddObjectGUID(_id.ToString());
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 18 - Bake Occlusion Culling
        // NO FIX
        public static AVRO_Settings.TicketStates GetBakeOcclusionCulling(AVRO_Ticket _ticket)
        {
            var _scene = EditorSceneManager.GetActiveScene();
            string _path = _scene.path;
            string _occlusionPath = _path.Replace(".unity", "/OcclusionCullingData.asset");
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_occlusionPath) == null ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        // Scene 19 - Activate Occlusion Culling on Camera
        public static AVRO_Settings.TicketStates GetActivateOcclusionCamera(AVRO_Ticket _ticket)
        {
            if (GetBakeOcclusionCulling(_ticket) != AVRO_Settings.TicketStates.Done)
                return AVRO_Settings.TicketStates.Omitted;
            _ticket.data.concernedObjects.Clear();
            if (Camera.main && Camera.main.useOcclusionCulling == false)
            {
                GlobalObjectId _id = GlobalObjectId.GetGlobalObjectIdSlow(Camera.main.gameObject);
                _ticket.AddObjectGUID(_id.ToString());
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetActivateOcclusionCamera(AVRO_Ticket _ticket)
        {
            if (GetBakeOcclusionCulling(_ticket) != AVRO_Settings.TicketStates.Done) return;
            Camera.main.useOcclusionCulling = true;
        }
        #endregion

        #region Files Settings
        //----------------------------------------
        //  FILES SETTINGS
        //----------------------------------------
        // Files 01 - Textures Crunch Compression
        public static AVRO_Settings.TicketStates GetTextureCrunchCompression(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    bool _isSupportedType = _textureImporter.textureType != TextureImporterType.Sprite && _textureImporter.textureType != TextureImporterType.NormalMap;
                    if (_isSupportedType && !_textureImporter.crunchedCompression)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetTextureCrunchCompression(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    bool _isSupportedType = _textureImporter.textureType != TextureImporterType.Sprite;
                    if (_isSupportedType)
                        _textureImporter.crunchedCompression = true;
                    EditorUtility.SetDirty(_textureImporter);
                }
            }
        }

        // Files 02 - Audios Force To Mono
        public static AVRO_Settings.TicketStates GetAudiosForceToMono(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _audiosGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (string _guid in _audiosGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_path) as AudioImporter;
                if (_audioImporter != null)
                {
                    if (!_audioImporter.forceToMono)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAudiosForceToMono(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_data.guid) as AudioImporter;
                if (_audioImporter != null)
                {
                    _audioImporter.forceToMono = true;
                    EditorUtility.SetDirty(_audioImporter);
                }
            }
        }

        // Files 03 - Audios Normalize
        public static AVRO_Settings.TicketStates GetAudiosNormalize(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _audiosGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (string _guid in _audiosGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_path) as AudioImporter;
                if (_audioImporter != null)
                {
                    var _serializedObject = new SerializedObject(_audioImporter);
                    var _normalize = _serializedObject.FindProperty("m_Normalize");
                    if (_normalize != null)
                    {
                        if (!_normalize.boolValue)
                            _ticket.AddObjectGUID(_path);
                    }
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAudiosNormalize(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_data.guid) as AudioImporter;
                if (_audioImporter != null)
                {
                    var _serializedObject = new SerializedObject(_audioImporter);
                    var _normalize = _serializedObject.FindProperty("m_Normalize");
                    if (_normalize != null)
                    {
                        _normalize.boolValue = true;
                        _serializedObject.ApplyModifiedProperties();
                        AssetDatabase.ImportAsset(_data.guid);
                        EditorUtility.SetDirty(_audioImporter);
                    }
                }
            }
        }

        // Files 04 - Audios Load Type
        public static AVRO_Settings.TicketStates GetAudiosLoadType(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _audiosGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });

            foreach (string _guid in _audiosGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_path) as AudioImporter;

                if (_audioImporter != null)
                {
                    string _fullPath = Path.Combine(Application.dataPath, _path.Replace("Assets/", ""));
                    if (!File.Exists(_fullPath))
                        continue;
                    float _fileSizeInKB = new FileInfo(_fullPath).Length / 1024f;
                    AudioClipLoadType _expectedLoadType;
                    if (_fileSizeInKB <= 300)
                        _expectedLoadType = AudioClipLoadType.DecompressOnLoad;
                    else if (_fileSizeInKB < 2048)
                        _expectedLoadType = AudioClipLoadType.CompressedInMemory;
                    else
                        _expectedLoadType = AudioClipLoadType.Streaming;
                    if (_audioImporter.defaultSampleSettings.loadType != _expectedLoadType)
                        _ticket.AddObjectGUID(_path, (int)_fileSizeInKB);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAudiosLoadType(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_data.guid) as AudioImporter;
                if (_audioImporter != null)
                {
                    var sampleSettings = _audioImporter.defaultSampleSettings;

                    string _fullPath = Path.Combine(Application.dataPath, _data.guid.Replace("Assets/", ""));
                    if (!File.Exists(_fullPath))
                        continue;
                    float _fileSizeInKB = new FileInfo(_fullPath).Length / 1024f;
                    AudioClipLoadType _expectedLoadType;
                    if (_fileSizeInKB <= 300f)
                        _expectedLoadType = AudioClipLoadType.DecompressOnLoad;
                    else if (_fileSizeInKB < 2048f)
                        _expectedLoadType = AudioClipLoadType.CompressedInMemory;
                    else
                        _expectedLoadType = AudioClipLoadType.Streaming;
                    if (sampleSettings.loadType != _expectedLoadType)
                    {
                        sampleSettings.loadType = _expectedLoadType;
                        _audioImporter.defaultSampleSettings = sampleSettings;
                        AssetDatabase.ImportAsset(_data.guid);
                        EditorUtility.SetDirty(_audioImporter);
                    }
                }
            }
        }

        // Files 05 - Audios Compression Format
        public static AVRO_Settings.TicketStates GetAudiosCompressionFormat(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _audiosGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (string _guid in _audiosGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_path) as AudioImporter;
                if (_audioImporter != null)
                {
                    if (_audioImporter.defaultSampleSettings.compressionFormat != AudioCompressionFormat.Vorbis)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAudiosCompressionFormat(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_data.guid) as AudioImporter;
                if (_audioImporter != null)
                {
                    var _sampleSettings = _audioImporter.defaultSampleSettings;
                    _sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                    EditorUtility.SetDirty(_audioImporter);
                }
            }
        }

        // Files 06 - Audios Quality
        public static AVRO_Settings.TicketStates GetAudiosQuality(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _audiosGUIDs = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (string _guid in _audiosGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_path) as AudioImporter;
                if (_audioImporter != null)
                {
                    if (_audioImporter.defaultSampleSettings.quality == 1)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAudiosQuality(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                AudioImporter _audioImporter = AssetImporter.GetAtPath(_data.guid) as AudioImporter;
                if (_audioImporter != null)
                {
                    var _sampleSettings = _audioImporter.defaultSampleSettings;
                    _sampleSettings.quality = AVRO_Utilities.Settings.DefaultAudioQuality;
                    EditorUtility.SetDirty(_audioImporter);
                }
            }
        }

        // Files 07 - Materials Simple Lit
        public static AVRO_Settings.TicketStates GetMaterialsSimpleLit(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP)
                return AVRO_Settings.TicketStates.Omitted;
            _ticket.data.concernedObjects.Clear();
            string[] _materialsGUIDs = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            foreach (string _guid in _materialsGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                Material _material = AssetDatabase.LoadAssetAtPath<Material>(_path);
                if (_material != null)
                {
                    if (_material.shader.name == "Universal Render Pipeline/Lit") // Standard for Built-in ?
                    {
                        if (_material.GetFloat("_EnvironmentReflections") == 0 && _material.GetFloat("_Metallic") == 0 && !_material.GetTexture("_ParallaxMap") &&
                        !_material.GetTexture("_DetailMask") && !_material.GetTexture("_DetailAlbedoMap") && !_material.GetTexture("_DetailNormalMap") && !_material.GetTexture("_OcclusionMap"))
                            _ticket.AddObjectGUID(_path);
                    }
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMaterialsSimpleLit(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP) return;
            foreach (var _data in _ticket.data.concernedObjects)
            {
                Material _material = AssetDatabase.LoadAssetAtPath<Material>(_data.guid);
                if (_material != null)
                {
                    _material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    EditorUtility.SetDirty(_material);
                }
            }
        }

        // Files 08 - Materials Render Face
        public static AVRO_Settings.TicketStates GetMaterialsRenderFace(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP)
                return AVRO_Settings.TicketStates.Omitted;
            _ticket.data.concernedObjects.Clear();
            string[] _materialsGUIDs = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            foreach (string _guid in _materialsGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                Material _material = AssetDatabase.LoadAssetAtPath<Material>(_path);
                if (_material != null)
                {
                    if (_material.HasFloat("_Cull") && _material.GetFloat("_Cull") == 0)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMaterialsRenderFace(AVRO_Ticket _ticket)
        {
            if (!AVRO_Utilities.IsUsingUnityURP) return;
            foreach (var _data in _ticket.data.concernedObjects)
            {
                Material _material = AssetDatabase.LoadAssetAtPath<Material>(_data.guid);
                if (_material != null)
                {
                    _material.SetFloat("_Cull", 2);
                    EditorUtility.SetDirty(_material);
                }
            }
        }

        // Files 09 - Textures Aniso Level
        public static AVRO_Settings.TicketStates GetTexturesAnisoLevel(AVRO_Ticket _ticket)
        {

            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.anisoLevel > 1)
                        _ticket.AddObjectGUID(_path, _textureImporter.anisoLevel);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetTexturesAnisoLevel(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    _textureImporter.anisoLevel = 1;
                    EditorUtility.SetDirty(_textureImporter);
                }
            }
        }

        // Files 10 - Textures Read Write
        public static AVRO_Settings.TicketStates GetTexturesReadWrite(AVRO_Ticket _ticket)
        {

            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.isReadable)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetTexturesReadWrite(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    _textureImporter.isReadable = false;
                    EditorUtility.SetDirty(_textureImporter);
                }
            }
        }

        // Files 11 - Textures Mipmaps
        public static AVRO_Settings.TicketStates GetTexturesMipmaps(AVRO_Ticket _ticket)
        {

            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    bool _isSupportedType = _textureImporter.textureType != TextureImporterType.Lightmap && _textureImporter.textureType != TextureImporterType.DirectionalLightmap &&
                    _textureImporter.textureType != TextureImporterType.Sprite && _textureImporter.textureType != TextureImporterType.GUI;
                    if (_isSupportedType && !_textureImporter.mipmapEnabled)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetTexturesMipmaps(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    bool _isSupportedType = _textureImporter.textureType != TextureImporterType.Lightmap && _textureImporter.textureType != TextureImporterType.DirectionalLightmap &&
                    _textureImporter.textureType != TextureImporterType.Sprite && _textureImporter.textureType != TextureImporterType.GUI;
                    if (_isSupportedType)
                        _textureImporter.mipmapEnabled = true;
                    EditorUtility.SetDirty(_textureImporter);
                }
            }
        }

        // Files 12 - Avoid 4K+ Textures
        public static AVRO_Settings.TicketStates GetAvoid4kTextures(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                bool _bigMaxSize = false;
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.maxTextureSize > 2048)
                        _bigMaxSize = true;
                }
                bool _bigSize = false;
                Texture2D _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
                if (_texture != null)
                {
                    if (_texture.width > 2048 || _texture.height > 2048)
                        _bigSize = true;
                }
                if (_bigMaxSize && _bigSize)
                    _ticket.AddObjectGUID(_path, _textureImporter.maxTextureSize);
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetAvoid4kTextures(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.maxTextureSize > 2048)
                        _textureImporter.maxTextureSize = 2048;
                }
            }
        }

        // Files 13 - Non Directional Lightmaps
        public static AVRO_Settings.TicketStates GetNonDirectionalLightmaps(AVRO_Ticket _ticket)
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings lighting_SettingsAsset))
                return AVRO_Settings.TicketStates.Omitted;
            return lighting_SettingsAsset.directionalityMode != LightmapsMode.NonDirectional ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetNonDirectionalLightmaps(AVRO_Ticket _ticket)
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings lighting_SettingsAsset)) return;
            lighting_SettingsAsset.directionalityMode = LightmapsMode.NonDirectional;
        }

        // Files 14 - Disable Realtime Global Illumination
        public static AVRO_Settings.TicketStates GetRealtimeGlobalIllumination(AVRO_Ticket _ticket)
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings lighting_SettingsAsset))
                return AVRO_Settings.TicketStates.Omitted;
            return lighting_SettingsAsset.realtimeGI ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetRealtimeGlobalIllumination(AVRO_Ticket _ticket)
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings lighting_SettingsAsset)) return;
            lighting_SettingsAsset.realtimeGI = false;
        }

        // Files 15 - Textures Filtering
        public static AVRO_Settings.TicketStates GetTexturesFiltering(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_path) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.filterMode == FilterMode.Trilinear)
                        _ticket.AddObjectGUID(_path, (int)_textureImporter.filterMode);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetTexturesFiltering(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                TextureImporter _textureImporter = AssetImporter.GetAtPath(_data.guid) as TextureImporter;
                if (_textureImporter != null)
                {
                    if (_textureImporter.filterMode == FilterMode.Trilinear)
                        _textureImporter.filterMode = FilterMode.Bilinear;
                }
            }
        }

        // Files 16 - Meshes Compression
        public static AVRO_Settings.TicketStates GetMeshesCompression(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _meshGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string _guid in _meshGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_path) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.meshCompression == ModelImporterMeshCompression.Off)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMeshesCompression(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_data.guid) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.meshCompression == ModelImporterMeshCompression.Off)
                        _modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
                }
            }
        }

        // Files 17 - Meshes Read Write
        public static AVRO_Settings.TicketStates GetMeshesReadWrite(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _meshGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string _guid in _meshGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_path) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.isReadable)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMeshesReadWrite(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_data.guid) as ModelImporter;
                if (_modelImporter != null)
                {
                    _modelImporter.isReadable = false;
                }
            }
        }

        // Files 18 - Disable Meshes BlendShapes
        public static AVRO_Settings.TicketStates GetMeshesBlendShapes(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _meshGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string _guid in _meshGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_path) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.importBlendShapes)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMeshesBlendShapes(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_data.guid) as ModelImporter;
                if (_modelImporter != null)
                {
                    _modelImporter.importBlendShapes = false;
                }
            }
        }

        // Files 19 - Disable Meshes Normals
        public static AVRO_Settings.TicketStates GetMeshesNormals(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _meshGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string _guid in _meshGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_path) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.importNormals != ModelImporterNormals.None)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMeshesNormals(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_data.guid) as ModelImporter;
                if (_modelImporter != null)
                {
                    _modelImporter.importNormals = ModelImporterNormals.None;
                }
            }
        }

        // Files 20 - Disable Meshes Tangents
        public static AVRO_Settings.TicketStates GetMeshesTangents(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _meshGUIDs = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string _guid in _meshGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_path) as ModelImporter;
                if (_modelImporter != null)
                {
                    if (_modelImporter.importTangents != ModelImporterTangents.None)
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }
        public static void SetMeshesTangents(AVRO_Ticket _ticket)
        {
            foreach (var _data in _ticket.data.concernedObjects)
            {
                ModelImporter _modelImporter = AssetImporter.GetAtPath(_data.guid) as ModelImporter;
                if (_modelImporter != null)
                {
                    _modelImporter.importTangents = ModelImporterTangents.None;
                }
            }
        }

        // Files 21 - Textures Power of 2
        // NO FIX
        public static AVRO_Settings.TicketStates GetTexturesPower(AVRO_Ticket _ticket)
        {
            _ticket.data.concernedObjects.Clear();
            string[] _textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string _guid in _textureGUIDs)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                Texture2D _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
                if (_texture != null)
                {
                    if (!Mathf.IsPowerOfTwo(_texture.width) || !Mathf.IsPowerOfTwo(_texture.height))
                        _ticket.AddObjectGUID(_path);
                }
            }
            return _ticket.data.concernedObjects.Count > 0 ? AVRO_Settings.TicketStates.Todo : AVRO_Settings.TicketStates.Done;
        }

        #endregion

        #region Physics Settings
        //----------------------------------------
        //  PHYSICS SETTINGS
        //----------------------------------------
        // Physics 01 - Default Contact Offset
        public static AVRO_Settings.TicketStates GetDefaultContactOffset(AVRO_Ticket _ticket)
        {
            return Physics.defaultContactOffset >= 0.01f ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetDefaultContactOffset(AVRO_Ticket _ticket)
        {
            Physics.defaultContactOffset = 0.01f;
        }

        // Physics 02 - Sleep Treshold
        public static AVRO_Settings.TicketStates GetSleepTreshold(AVRO_Ticket _ticket)
        {
            return Physics.sleepThreshold >= 0.005f ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetSleepTreshold(AVRO_Ticket _ticket)
        {
            Physics.sleepThreshold = 0.005f;
        }

        // Physics 03 - Default Solver Iterations
        public static AVRO_Settings.TicketStates GetDefaultSolverIterations(AVRO_Ticket _ticket)
        {
            return Physics.defaultSolverIterations <= 8 ? AVRO_Settings.TicketStates.Done : AVRO_Settings.TicketStates.Todo;
        }
        public static void SetDefaultSolverIterations(AVRO_Ticket _ticket)
        {
            Physics.defaultSolverIterations = 6;
        }
        #endregion
    }
}