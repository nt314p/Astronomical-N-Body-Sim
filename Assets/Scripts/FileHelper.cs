using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FileHelper
{
    private const int SizeOfPointMassState = 40;
    private static byte[] pointMassesBuffer = new byte[65536 * SizeOfPointMassState];
    
    public static bool SaveScreenshot(Texture2D texture)
    {
        var bytes = texture.EncodeToPNG();
        var maxScreenshotNum = -1;
        
        if (!Directory.Exists("Screenshots"))
        {
            Directory.CreateDirectory("Screenshots");
        }
        var files = Directory.GetFiles("Screenshots/", "screenshot*.png");

        foreach (var file in files)
        {
            var numStr = file.Substring(22, file.Length - 26);
            if (int.TryParse(numStr, out var screenshotNum))
            {
                if (screenshotNum > maxScreenshotNum)
                    maxScreenshotNum = screenshotNum;
            }
        }

        maxScreenshotNum++;
        
        var path = "Screenshots/screenshot" + maxScreenshotNum + ".png";
        File.WriteAllBytes(path, bytes);
        return true;
    }

    public static SimulationState LoadSimulationState()
    {
        var path = "SavedStates/saved_state.simstate";
        var stateFile = File.Open(path, FileMode.Open);
        using var binaryReader = new BinaryReader(stateFile);
        var numMasses = stateFile.Length / SizeOfPointMassState;
        var pointMasses = new PointMassState[numMasses];
        byte[] pointMassBuffer;
        var pointMassPointer = Marshal.AllocHGlobal(SizeOfPointMassState);
        for (var index = 0; index < numMasses; index++)
        {
            pointMassBuffer = binaryReader.ReadBytes(SizeOfPointMassState);
            Marshal.Copy(pointMassBuffer, 0, pointMassPointer, SizeOfPointMassState);
            pointMasses[index] = (PointMassState) Marshal.PtrToStructure(pointMassPointer, typeof(PointMassState));
        }

        return new SimulationState(pointMasses);
    }

    public static void SaveSimulationState(AstronomicalSimulator astronomicalSimulator)
    {
        var path = "SavedStates/saved_state.simstate";
        if (!Directory.Exists("SavedStates"))
        {
            Directory.CreateDirectory("SavedStates");
        }

        var pointMassPointer = Marshal.AllocHGlobal(SizeOfPointMassState);
        var numMasses = astronomicalSimulator.NumMasses;
        astronomicalSimulator.GetSimulationStateNonAllocBytes(pointMassesBuffer);
        
        using var binaryWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        binaryWriter.Write(pointMassesBuffer, 0, numMasses * SizeOfPointMassState);
        
        Marshal.FreeHGlobal(pointMassPointer);
    }

    public static PointMassState ReadPointMassState(BinaryReader binaryReader)
    {
        return new PointMassState
        {
            Mass = binaryReader.ReadSingle(),
            Position = binaryReader.ReadVector3(),
            Velocity = binaryReader.ReadVector3(),
            Acceleration = binaryReader.ReadVector3()
        };
    }

    private static void WritePointMassState(BinaryWriter binaryWriter, PointMassState pointMassState)
    {
        binaryWriter.Write(pointMassState.Mass);
        binaryWriter.WriteVector3(pointMassState.Position);
        binaryWriter.WriteVector3(pointMassState.Velocity);
        binaryWriter.WriteVector3(pointMassState.Acceleration);
    }

    private static Vector3 ReadVector3(this BinaryReader binaryReader)
    {
        return new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
    }

    private static void WriteVector3(this BinaryWriter binaryWriter, Vector3 vector3)
    {
        binaryWriter.Write(vector3.x);
        binaryWriter.Write(vector3.y);
        binaryWriter.Write(vector3.z);
    }
}
