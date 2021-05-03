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
        catch(Exception e)
        {
            Debug.Log("Captured exception");
        }
        finally
        {
            OtherMethod(false);
        } 
        
    }

    private void OtherMethod(bool t)
    {
        if (t) throw new Exception("Test throw");
        Nested();

        void Nested()
        {
            
        }

        void AnotherNested(int i)
        {
            void SecondaryNesting(){}
            SecondaryNesting();
        }
        AnotherNested(12);
    }
}
