using System;
using System.IO;
using UnityEngine;

public static class FileHelper
{
    private const int SizeOfPointMassState = 40;
    private const int SizeOfStreamPointMassState = 16; // Vector3 position, float speed
    private static readonly byte[] PointMassesBuffer = new byte[65536 * SizeOfPointMassState];
    private static readonly byte[] StreamingBuffer = new byte[65536 * SizeOfStreamPointMassState];
    private static readonly byte[] StreamingMasses = new byte[65536 * sizeof(float)];
    public static bool IsRecording { get; private set; }
    public static bool IsReplaying { get; private set; }
    private static BinaryWriter recordingWriter;
    private static BinaryReader replayReader;
    private static int replayingNumMasses;
    public static int ReplayStep;
    private static AstronomicalSimulator currentAstronomicalSimulator;

    public static void InitializeDirectories()
    {
        if (!Directory.Exists("Screenshots"))
            Directory.CreateDirectory("Screenshots");
        
        if (!Directory.Exists("Saves"))
            Directory.CreateDirectory("Saves");
    }
    
    public static void SaveScreenshot(Texture2D texture)
    {
        var bytes = texture.EncodeToPNG();
        var maxScreenshotNum = -1;
        
        var files = Directory.GetFiles("Screenshots/", "screenshot*.png");

        foreach (var file in files)
        {
            var numStr = file.Substring(22, file.Length - 26);
            if (!int.TryParse(numStr, out var screenshotNum)) continue;
            
            if (screenshotNum > maxScreenshotNum)
                maxScreenshotNum = screenshotNum;
        }

        maxScreenshotNum++;
        
        var path = "Screenshots/screenshot" + maxScreenshotNum + ".png";
        File.WriteAllBytes(path, bytes);
    }

    public static void StartStateReplay(string fileName, AstronomicalSimulator astronomicalSimulator)
    {
        if (IsReplaying)
        {
            throw new InvalidOperationException("Replay already started");
        }
        
        if (IsRecording)
        {
            throw new InvalidOperationException("Cannot start replay when recording");
        }

        var path = $"Saves/{fileName}.simstream";
        var replayFile = File.Open(path, FileMode.Open);

        replayReader = new BinaryReader(replayFile);
        replayingNumMasses = replayReader.ReadInt32();

        replayReader.Read(StreamingMasses, 0, replayingNumMasses * sizeof(float));

        currentAstronomicalSimulator = astronomicalSimulator;
        IsReplaying = true;
        ReplayStep = 1;
        UpdateStateReplay();
        ReplayStep = 0;
    }

    public static void UpdateStateReplay()
    {
        if (ReplayStep == 0) return;
        if (!IsReplaying)
        {
            throw new InvalidOperationException("Replay has not been started");
        }
        
        // Read in strides of 16: 12 bytes position, 4 bytes speed
        var stateSize = replayingNumMasses * SizeOfStreamPointMassState;
        replayReader.Read(StreamingBuffer, 0, stateSize);
        
        // Populate simulation state buffer of stride 40: 4 bytes mass, 12 bytes per position, velocity, acceleration
        for (var index = 0; index < replayingNumMasses; index++)
        {
            var offset = index * SizeOfPointMassState;
            var massOffset = index * sizeof(float);
            var positionVelocityOffset = index * SizeOfStreamPointMassState;
            
            FloatByteBufferCopy(StreamingMasses, massOffset, PointMassesBuffer, offset); // Copy mass
            offset += sizeof(float);
            
            for (var positionVelocityIndex = 0; // Position and velocity.x
                positionVelocityIndex < SizeOfStreamPointMassState; // TODO: I swear this is bugged
                positionVelocityIndex++)
            {
                PointMassesBuffer[offset + positionVelocityIndex] =
                    StreamingBuffer[positionVelocityOffset + positionVelocityIndex];
            }

            offset += SizeOfStreamPointMassState;
            const int numZeros = SizeOfPointMassState - SizeOfStreamPointMassState - sizeof(float);

            for (var zeroIndex = 0; zeroIndex < numZeros; zeroIndex++) // Velocity.yz, Acceleration
            {
                PointMassesBuffer[offset + zeroIndex] = 0;
            }
        }

        try
        {
            
            if (replayReader.BaseStream.Position < replayReader.BaseStream.Length || ReplayStep < 0)
            {
                replayReader.BaseStream.Position += (ReplayStep - 1) * stateSize;
            }
            else
            {
                replayReader.BaseStream.Position = replayReader.BaseStream.Length;
                ReplayStep = 0;
                throw new InvalidOperationException("Reached end of replay");
            }
        }
        catch (ArgumentOutOfRangeException) // beginning of replay
        {
            ReplayStep = 0;
            throw;
        }

        currentAstronomicalSimulator.SetSimulationStateNonAllocBytes(PointMassesBuffer, replayingNumMasses);
    }

    public static void EndStateReplay()
    {
        if (!IsReplaying)
        {
            throw new InvalidOperationException("Replay has not been started");
        }
        replayReader.Close();
        IsReplaying = false;
    }

    public static void StartStateRecording(string fileName, AstronomicalSimulator astronomicalSimulator)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Recording already started");
        }

        if (IsReplaying)
        {
            throw new InvalidOperationException("Cannot start recording when replaying");
        }

        var path = $"Saves/{fileName}.simstream";
        recordingWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        currentAstronomicalSimulator = astronomicalSimulator;
        recordingWriter.Write(currentAstronomicalSimulator.NumMasses); // Write number of masses (4 bytes)
        
        // recordingWriter.Write(currentAstronomicalSimulator.); // write timestep

        astronomicalSimulator.GetSimulationStateNonAllocBytes(PointMassesBuffer);
        
        // Write mass values. The mass values are assumed to not change over the simulation.
        for (var index = 0; index < currentAstronomicalSimulator.NumMasses; index++) 
        {
            var offset = index * SizeOfPointMassState;
            var streamingOffset = index * sizeof(float);
            FloatByteBufferCopy(PointMassesBuffer, offset, StreamingMasses, streamingOffset);
        }

        recordingWriter.Write(StreamingMasses, 0, currentAstronomicalSimulator.NumMasses * sizeof(float));
        
        IsRecording = true;
    }

    public static void UpdateStateRecording()
    {
        if (!IsRecording)
        {
            throw new InvalidOperationException("Recording has not been started");
        }
        
        currentAstronomicalSimulator.GetSimulationStateNonAllocBytes(PointMassesBuffer);

        for (var index = 0; index < currentAstronomicalSimulator.NumMasses; index++)
        {
            var offset = index * SizeOfPointMassState + sizeof(float); // Offset a single float (mass value) from start
            var streamOffset = index * SizeOfStreamPointMassState;
            
            for (var positionIndex = 0; positionIndex < 3 * sizeof(float); positionIndex++) // Position
            {
                StreamingBuffer[streamOffset + positionIndex] = PointMassesBuffer[offset + positionIndex];
            }
            
            var squareMagnitudeVelocity = 0.0f; // flatten velocity to single float
            for (var velocityComponent = 0; velocityComponent < 3; velocityComponent++)
            {
                var component = BitConverter.ToSingle(PointMassesBuffer, offset + velocityComponent * sizeof(float));
                squareMagnitudeVelocity += component * component;
            }

            streamOffset += 3 * sizeof(float);
            
            var velocityBytes = BitConverter.GetBytes(Mathf.Sqrt(squareMagnitudeVelocity));
            FloatByteBufferCopy(velocityBytes, 0, StreamingBuffer, streamOffset); // Velocity
        }
        recordingWriter.Write(StreamingBuffer, 0, currentAstronomicalSimulator.NumMasses * SizeOfStreamPointMassState);
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

    public static void CloseFiles()
    {
        recordingWriter?.Close();
        replayReader?.Close();
    }

    public static void LoadSimulationState(string fileName, AstronomicalSimulator astronomicalSimulator)
    {
        var path = $"Saves/{fileName}.simstate";
        var stateFile = File.Open(path, FileMode.Open);
        
        using var binaryReader = new BinaryReader(stateFile);
        var numMasses = (int) stateFile.Length / SizeOfPointMassState;
        binaryReader.Read(PointMassesBuffer, 0, (int) stateFile.Length);
        
        astronomicalSimulator.SetSimulationStateNonAllocBytes(PointMassesBuffer, numMasses);
    }

    public static void SaveSimulationState(string fileName, AstronomicalSimulator astronomicalSimulator)
    {
        var path = $"Saves/{fileName}.simstate";
        
        var numMasses = astronomicalSimulator.NumMasses;
        astronomicalSimulator.GetSimulationStateNonAllocBytes(PointMassesBuffer);
        
        using var binaryWriter = new BinaryWriter(File.Open(path, FileMode.Create));
        binaryWriter.Write(PointMassesBuffer, 0, numMasses * SizeOfPointMassState);
    }
    
    // Copies four bytes (a float) from the source buffer at the source offset to the destination buffer at the destination offset
    private static void FloatByteBufferCopy(byte[] src, int srcOffset, byte[] dest, int destOffset)
    {
        dest[destOffset] = src[srcOffset];
        dest[destOffset + 1] = src[srcOffset + 1];
        dest[destOffset + 2] = src[srcOffset + 2];
        dest[destOffset + 3] = src[srcOffset + 3];
    }
}
