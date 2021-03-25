using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class BasicBehaviour : MonoBehaviour
{
    public int lineCount;
    
    void Update()
    {
        lineCount = MyCall(5000);
    }

    private string thing;

    public void EmptyMethod_MustFail()
    {
        
    }

    public int MethodWithNoCalls_MustFail()
    {
        int a = 2;
        int b = 3;
        return a + b;
    }
    
    public void MethodWithCallsInsideTry_MustFail()
    {
        try
        {
            MyInternalCall01();
        }
        catch (System.Exception e)
        {
            
        }
    }

    public void MethodWithCallsAfterTry_MustSucceed()
    {
        try
        {
            MyInternalCall01();
        }
        catch (System.Exception e)
        {
            
        }
        
        MyInternalCall02();
    }
    
    public void MethodWithCallsBeforeTry_MustSucceed()
    {
        MyInternalCall02();
        
        try
        {
            MyInternalCall01();
        }
        catch (System.Exception e)
        {
            
        }
    }
    
    public void MethodWithCallsInsideCatch_MustFail()
    {
        try
        {
            MyInternalCall01();
        }
        catch (System.Exception e)
        {
            MyInternalCall02();
        }
    }
    
    public void MethodWithCallsInsideNestedTry_MustFail()
    {
        try
        {
            MyInternalCall01();
            try
            {
                MyInternalCall02();
            }
            catch (System.Exception e)
            {
                
            }
        }
        catch (System.Exception e)
        {
            
        }
    }
    
    public static int MyStaticCall()
    {
        return MyInternalStaticCall();
    }

    static int MyInternalStaticCall()
    {
        return Random.Range(0, 10);
    }

    // Update is called once per frame
    public int MyCall(int lines)
    {
        var sb = new StringBuilder();
        
        for (int i = 0; i < lines; i++)
        {
            sb.AppendLine(i.ToString());
        }

        MyInternalCall01();
        MyInternalCall02();
        sb.AppendLine(thing);

        return sb.Length;
    }

    private void MyInternalCall01()
    {
        thing += "a";
    }

    private void MyInternalCall02()
    {
        thing += "b";
    }
}
