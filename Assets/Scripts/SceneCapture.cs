using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class SceneCapture : MonoBehaviour
{
    public static SceneCapture Instance { get; private set; }
    private string ffmpegPath = "/Users/ashar/Desktop/repo/spoken-to-signed-translation/unity3d/mac/ffmpeg";
    private int frameRate = 25;

    private int width;
    private int height;
    private string outputFilePath;
    private Texture2D screenTexture;
    private RenderTexture renderTexture;
    private Coroutine recordingCoroutine;
    private bool isRecording;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        width = Screen.width;
        height = Screen.height;
        if (height % 2 != 0)
        {
            height += 1;
        }
        // Initialize textures with the current screen dimensions
        screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        renderTexture = new RenderTexture(width, height, 24);
    }

    void OnDestroy()
    {
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
        }
    }

    public void SetOutputFilePath(string path)
    {
        outputFilePath = path;
    }

    public void StartRecording()
    {
        if (recordingCoroutine == null)
        {
            isRecording = true;
            recordingCoroutine = StartCoroutine(Record());
        }
    }

    public void StopRecording()
    {
        if (recordingCoroutine != null)
        {
            isRecording = false;
            //StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
    }

    private IEnumerator Record()
    {
        if (File.Exists(outputFilePath))
        {
            File.Delete(outputFilePath);
        }

        Process ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = ffmpegPath;
        ffmpegProcess.StartInfo.Arguments = $"-y -f rawvideo -pixel_format rgb24 -video_size {width}x{height} -framerate {frameRate} -i - -c:v libx264 -pix_fmt yuv420p -preset ultrafast \"{outputFilePath}\"";
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.RedirectStandardInput = true;
        ffmpegProcess.StartInfo.CreateNoWindow = true;
        //ffmpegProcess.StartInfo.RedirectStandardOutput = true;
        //ffmpegProcess.StartInfo.RedirectStandardError = true;
        try
        {
            ffmpegProcess.Start();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to start FFmpeg process: {e.Message}");
            yield break;
        }

        IntPtr buffer = Marshal.AllocHGlobal(width * height * 3);

        while (isRecording)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                CaptureFrame(buffer);
                byte[] rawData = screenTexture.GetRawTextureData();
                ffmpegProcess.StandardInput.BaseStream.Write(rawData, 0, rawData.Length);
                ffmpegProcess.StandardInput.BaseStream.Flush();
            }
            catch (Exception e)
            {
                //string output = ffmpegProcess.StandardOutput.ReadToEnd();
                //string error = ffmpegProcess.StandardError.ReadToEnd();
                //UnityEngine.Debug.Log($"FFmpeg output: {output}");
                //UnityEngine.Debug.Log($"FFmpeg error: {error}");
                UnityEngine.Debug.LogError($"Error during recording: {e.Message}");
            }
        }

        ffmpegProcess.StandardInput.Close();
        ffmpegProcess.WaitForExit();
        Marshal.FreeHGlobal(buffer);
    }

    private void CaptureFrame(IntPtr buffer)
    {
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);
        Graphics.Blit(null, renderTexture);

        screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenTexture.Apply();

        Marshal.Copy(screenTexture.GetRawTextureData(), 0, buffer, width * height * 3);
    }
}
