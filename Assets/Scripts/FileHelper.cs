using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FileHelper
{
    private const int SizeOfPointMassState = 40;
    private static readonly byte[] PointMassesBuffer = new byte[65536 * SizeOfPointMassState];
    public static bool IsRecording { get; private set; } = false;
    public static bool IsStreaming { get; private set; } = false;
    private static BinaryWriter recordingWriter;
    private static BinaryReader streamingReader;
    private static int streamingNumMasses;
    private static AstronomicalSimulator currentAstronomicalSimulator;
    
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

    public static void StartStateStreaming(AstronomicalSimulator astronomicalSimulator)
    {
        if (IsStreaming)
        {
            throw new InvalidOperationException("Streaming already started");
        }
        
        if (IsRecording)
        {
            throw new InvalidOperationException("Cannot start streaming when recording");
        }

        var path = "Saves/saved_stream.simstream";
        streamingReader = new BinaryReader(File.Open(path, FileMode.Open));
        streamingNumMasses = streamingReader.ReadInt32();
        currentAstronomicalSimulator = astronomicalSimulator;
        Debug.Log("Num mass stream size is: " + streamingNumMasses);
        IsStreaming = true;
    }

    public static void UpdateStateStreaming(int step)
    {
        if (step == 0) return;
        if (!IsStreaming)
        {
            throw new InvalidOperationException("Streaming has not been started");
        }

        var stateSize = streamingNumMasses * SizeOfPointMassState;
        streamingReader.Read(PointMassesBuffer, 0, stateSize);
        //streamingReader.BaseStream.Position += (step - 1) * stateSize;
        currentAstronomicalSimulator.SetSimulationStateNonAllocBytes(PointMassesBuffer, streamingNumMasses);
    }

    public static void EndStateStreaming()
    {
        if (!IsStreaming)
        {
            throw new InvalidOperationException("Streaming has not been started");
        }
        streamingReader.Close();
        IsStreaming = false;
    }

    public static void StartStateRecording(AstronomicalSimulator astronomicalSimulator)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Recording already started");
        }

        if (IsStreaming)
        {
            throw new InvalidOperationException("Cannot start recording when streaming");
        }

        var path = "Saves/saved_stream.simstream";
        recordingWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        currentAstronomicalSimulator = astronomicalSimulator;
        recordingWriter.Write(currentAstronomicalSimulator.NumMasses);
        // recordingWriter.Write(currentAstronomicalSimulator.); // write timestep
        IsRecording = true;
    }

    public static void UpdateStateRecording()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Recording has not been started");
        }
        
        currentAstronomicalSimulator.GetSimulationStateNonAllocBytes(PointMassesBuffer);
        recordingWriter.Write(PointMassesBuffer, 0, currentAstronomicalSimulator.NumMasses * SizeOfPointMassState);
    }

    public static void EndStateRecording()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Recording has not been started");
        }
        recordingWriter.Close();
        IsRecording = false;
    }

    public static void LoadSimulationState(AstronomicalSimulator astronomicalSimulator)
    {
        var path = "Saves/saved_state.simstate";
        var stateFile = File.Open(path, FileMode.Open);
        using var binaryReader = new BinaryReader(stateFile);
        var numMasses = (int) stateFile.Length / SizeOfPointMassState;
        binaryReader.Read(PointMassesBuffer, 0, (int) stateFile.Length);
        
        astronomicalSimulator.SetSimulationStateNonAllocBytes(PointMassesBuffer, numMasses);
    }

    public static void SaveSimulationState(AstronomicalSimulator astronomicalSimulator)
    {
        var path = "Saves/saved_state.simstate";
        if (!Directory.Exists("SavedStates"))
        {
            Directory.CreateDirectory("Saves");
        }

        var numMasses = astronomicalSimulator.NumMasses;
        astronomicalSimulator.GetSimulationStateNonAllocBytes(PointMassesBuffer);
        
        using var binaryWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        binaryWriter.Write(PointMassesBuffer, 0, numMasses * SizeOfPointMassState);
    }
}
