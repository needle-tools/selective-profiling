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
        catch(TestException e)
        {
            // Debug.Log("Captured expected exception");
        }
        finally
        {
            OtherMethod(false);
        } 
        
    }
    
    private class TestException : Exception{}

    private void OtherMethod(bool t)
    {
        if (t) throw new TestException();
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
