using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fragilem17.MirrorsAndPortals
{
    /**
     * Used to transport PortalableObjects to the linked portal defined in the portalSurface
     */
    public class PortalTransporter : MonoBehaviour
    {
        private Portal _portal;
        public List<PortalableObject> portalableObjects = new List<PortalableObject>();
        //private List<CloneRenderer> cloneObjects = new List<CloneRenderer>();
        public HashSet<CloneRenderer> cloneObjects = new HashSet<CloneRenderer>();
        public HashSet<CloneRenderer> cloneObjectsActuallyTouching = new HashSet<CloneRenderer>();
        public HashSet<CloneRenderer> cloneObjectsToRemove = new HashSet<CloneRenderer>();
        private List<PortalAffectorSphere> portalAffectorSpheres = new List<PortalAffectorSphere>();
        private Vector3 _originalScale;

        [Space(10)]
        [Header("Events")]
        public UnityEvent<PortalableObject> OnObjectEnteredPortal;
        public UnityEvent<PortalableObject> OnObjectTransportedAwayFromHere;
        public UnityEvent<PortalableObject> OnObjectTransportedToHere;
        public UnityEvent<PortalableObject> OnObjectExitedPortal;

        [HideInInspector]
        public Collider MyCollider;

        public Portal Portal { get => _portal; }

        public void Initialise()
        {
            MyCollider = GetComponent<Collider>();
            if (!MyCollider)
            {
                Debug.LogWarning(PortalUtils.Colorize("[PORTALS] ", PortalUtils.DebugColors.Warn, true) + name + " has a PortalTransporter Component but no trigger Collider, add a big enough Collider for things to hit it before reaching the actual portal.");
            }
            else
            {
                MyCollider.isTrigger = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (!rb)
            {
                Debug.LogWarning(PortalUtils.Colorize("[PORTALS] ", PortalUtils.DebugColors.Warn, true) + name + " has a PortalTransporter Component but no RigidBody Component. Add a (Kinematic) Rigidbody Component to this GameObject.");
            }

            _portal = GetComponentInParent<Portal>();
            if (!_portal)
            {
                Debug.LogWarning(PortalUtils.Colorize("[PORTALS] ", PortalUtils.DebugColors.Warn, true) + name + " has a PortalTransporter Component but no Portal Component, add a Portal Component to this or a parent GameObject.");
            }
        }

        protected void OnEnable()
        {
            Initialise();       
        }

        protected void Update()
        {
            PortalableObject p = null;
            for (int i = 0; i < portalableObjects.Count; ++i)
            {
                p = portalableObjects[i];
                if (p)
                {
                    Vector3 objPos = _portal.PortalSurface.transform.InverseTransformPoint(p.transform.position);

                    if (objPos.z > 0)
                    {
                        PortalableObject po = portalableObjects[i];
                        if (po.CanPortal())
                        {
                            //Debug.Log("PortalTransporter fixedUpdate z > 0 - doing portal on " + po.name);
                            if (_portal.OtherPortal && _portal.OtherPortal.PortalTransporter)
                            {
                                _portal.OtherPortal.PortalTransporter.ExternalTriggerEnter(po.gameObject);
                                _portal.OtherPortal.PortalTransporter.OnObjectTransportedToHere.Invoke(po);
                            }
                            po.PortalFrom(_portal);
                            OnObjectTransportedAwayFromHere.Invoke(po);

                        }

                    }
                }
            }
        }

        public void ExternalTriggerEnter(GameObject other, bool fromPhysics = false)
        {
            // first trigger the exit in the other portal
            if (_portal.OtherPortal && _portal.OtherPortal.PortalTransporter)
            {
                _portal.OtherPortal.PortalTransporter.ExternalTriggerExit(other, fromPhysics);
            }

            Vector3 objPos = _portal.PortalSurface.transform.InverseTransformPoint(other.transform.position);
            // did we enter in front of the portal?
            //Debug.Log("ExternalTriggerEnter objPos: " + other.name + " " + objPos.z);
            if (!fromPhysics || (fromPhysics && objPos.z < 0))
            {
                PortalableObject obj = other.GetComponent<PortalableObject>();
                if (obj && obj.isActiveAndEnabled && !portalableObjects.Contains(obj))
                {
                    //Debug.Log("PortalableObject " + obj.name + " SetIsInPortal: " + _portal.name + " fromPhysics: " + fromPhysics);
                    if (fromPhysics)
                    {
                        //Debug.Log("Adding to portal list of " + _portal.name + " object:" + other.name);
                        portalableObjects.Add(obj);
                    }
                    obj.SetIsInPortal(_portal);
                    OnObjectEnteredPortal.Invoke(obj);
                }


                CloneRenderer cloneObject = other.GetComponent<CloneRenderer>();
                if (cloneObject && cloneObject.isActiveAndEnabled && !cloneObjects.Contains(cloneObject))
                {
                    cloneObjects.Add(cloneObject);
                    cloneObject.SetIsInPortal(_portal, true);
                }

                PortalAffectorSphere affectorSphere = other.GetComponent<PortalAffectorSphere>();
                if (affectorSphere && affectorSphere.isActiveAndEnabled && !portalAffectorSpheres.Contains(affectorSphere))
                {
                    if (fromPhysics)
                    {
                        portalAffectorSpheres.Add(affectorSphere);
                    }
                    affectorSphere.SetIsInPortal(_portal);
                }
            }
        }


        public void ExternalTriggerExit(GameObject other, bool fromPhysics = false)
        {
            CloneRenderer cloneObject = other.GetComponent<CloneRenderer>();
            if (cloneObject && cloneObjects.Contains(cloneObject))
            {
                cloneObjects.Remove(cloneObject);
                cloneObject.ExitPortal(_portal, fromPhysics);
            }

            PortalableObject portalableObj = other.GetComponent<PortalableObject>();
            if (portalableObj && portalableObjects.Contains(portalableObj))
            {
                //Debug.Log("Exit Portal " + gameObject.name);
                //Debug.Log("Removing from portal list of " + _portal.name + " object:" + other.name);
                //Debug.Log("PortalableObject " + portalableObj.name + " ExitsPortal: " + _portal.name + " fromPhysics: " + fromPhysics);
                portalableObjects.Remove(portalableObj);
                portalableObj.ExitPortal(_portal);
                OnObjectExitedPortal.Invoke(portalableObj);
            }

            PortalAffectorSphere affectorSphere = other.GetComponent<PortalAffectorSphere>();
            if (affectorSphere && portalAffectorSpheres.Contains(affectorSphere))
            {
                portalAffectorSpheres.Remove(affectorSphere);
                affectorSphere.ExitPortal(_portal);
            }
        }

        protected void OnTriggerEnter(Collider other)
        {
            //Debug.Log("OnTriggerEnter: " + other.name);
            ExternalTriggerEnter(other.gameObject, true);
        }

        protected void OnTriggerExit(Collider other)
        {
            ExternalTriggerExit(other.gameObject, true);
        }
    }
}