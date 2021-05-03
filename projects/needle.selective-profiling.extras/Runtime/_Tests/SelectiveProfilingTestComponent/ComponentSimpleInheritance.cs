using System.Threading;
using UnityEngine;



public class ComponentSimpleInheritance : SimpleBaseComponent
{
    protected override void Update()
    {
        base.Update();
        Thread.Sleep(1);
    }

    protected override void AnotherCall()
    {
        
    }
}



public abstract class SimpleBaseComponent : MonoBehaviour
{
    protected  virtual void Update()
    {
        BaseMethodCall();
        AnotherCall();
    }
        
    private void BaseMethodCall()
    {
        Thread.Sleep(1);
    }

    protected abstract void AnotherCall();
}
