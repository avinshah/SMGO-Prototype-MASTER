using UnityEngine;
using UnityEngine.Events;

public class ZoneRelay : MonoBehaviour
{
    public enum Kind { Base, Handle }
    public Kind kind;

    public UnityEvent<Collider> onEnter;
    public UnityEvent<Collider> onExit;

    Collider _self;
    void Awake() { _self = GetComponent<Collider>(); }

    void OnTriggerEnter(Collider other) { onEnter?.Invoke(_self); }
    void OnTriggerExit(Collider other) { onExit?.Invoke(_self); }
}
