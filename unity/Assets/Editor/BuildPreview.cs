using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAIby.Editor
{
    public static class BuildPreview
    {
        [MenuItem("BAIby/Build Browser Preview")]
        public static void BuildWeb()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new BuildFailedException("Enable the Nursery scene in Build Profiles before building.");

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install Web Build Support for this Unity editor.");

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "Web"));
            var previousCompression = PlayerSettings.WebGL.compressionFormat;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                });

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(Path.Combine(output, "index.html")))
                    throw new BuildFailedException($"Browser preview build failed: {report.summary.result}.");

                Debug.Log($"BAIby browser preview written to {output}");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = previousCompression;
            }
        }
    }
}
