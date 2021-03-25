using UnityEngine;

[ExecuteInEditMode]
public class RepaintEverythingEveryFrame : MonoBehaviour
{
#if UNITY_EDITOR
    void Update()
    {
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif
}