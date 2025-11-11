using UnityEngine;
using UnityEngine.EventSystems;

public class SanityScript : MonoBehaviour
{
    void Start()
    {
        var es = EventSystem.current;
        Debug.Log(es ? "[Check] EventSystem found." : "[Check] ❌ No EventSystem in scene.");

        if (es)
        {
            var mod = es.currentInputModule;
            Debug.Log(mod ? $"[Check] Input Module: {mod.GetType().Name}"
                          : "[Check] ❌ EventSystem has no active input module (timing or setup).");
        }

        var cam = Camera.main;
        if (!cam) { Debug.Log("[Check] ❌ No Camera.main."); return; }
        var pr = cam.GetComponent<PhysicsRaycaster>();
        Debug.Log(pr ? "[Check] PhysicsRaycaster on main camera." : "[Check] ❌ Add PhysicsRaycaster to main camera.");
    }
}
