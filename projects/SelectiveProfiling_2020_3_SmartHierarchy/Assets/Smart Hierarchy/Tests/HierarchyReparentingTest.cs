using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HierarchyReparentingTest : MonoBehaviour
{
    public Transform[] targets;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        for (var i = 0; i < targets.Length; i++)
        {
            targets[i].SetParent(transform);
        }
        
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            var index = Random.Range(0, targets.Length - 1);

            if (index == i)
                continue;

            target.SetParent(targets[index]);
        }
    }
}
