using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ComponentWithTryCatch : MonoBehaviour
{
    public void Update()
    {
        try
        {
            OtherMethod(true);
        }
        catch{}
        finally
        {
            OtherMethod(false);
        } 
        
    }

    private void OtherMethod(bool t)
    {
        if (t) throw new Exception("Test throw");
        else Nested();

        void Nested()
        {
            
        }
        
        
    }
}
