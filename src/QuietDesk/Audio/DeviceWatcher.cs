using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace QuietDesk;
internal sealed class DeviceWatcher : IMMNotificationClient,IDisposable
{
    private readonly MMDeviceEnumerator enumerator=new();private readonly Action changed;private bool disposed;
    internal DeviceWatcher(Action changed){this.changed=changed;enumerator.RegisterEndpointNotificationCallback(this);}
    public void OnDefaultDeviceChanged(DataFlow flow,Role role,string id){if(!disposed&&flow==DataFlow.Render&&role==Role.Console)changed();}
    public void OnDeviceStateChanged(string id,DeviceState state){if(!disposed)changed();}
    public void OnDeviceAdded(string id){if(!disposed)changed();}
    public void OnDeviceRemoved(string id){if(!disposed)changed();}
    public void OnPropertyValueChanged(string id,PropertyKey key){}
    public void Dispose(){disposed=true;enumerator.UnregisterEndpointNotificationCallback(this);enumerator.Dispose();}
}
