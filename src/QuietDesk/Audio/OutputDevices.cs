using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace QuietDesk;
public sealed record OutputDevice(string? Id,string Name){public override string ToString()=>Name;}
internal static class OutputDevices
{
    internal static List<OutputDevice> List(){var result=new List<OutputDevice>{new(null,"跟随系统默认")};using var enumerator=new MMDeviceEnumerator();foreach(var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)){using(device)result.Add(new(device.ID,device.FriendlyName));}return result;}
}
