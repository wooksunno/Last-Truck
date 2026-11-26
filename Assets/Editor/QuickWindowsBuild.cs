using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 친구에게 보낼 윈도우 빌드를 한 번에 만든다 (메뉴: LastTruck > Build > Windows 빌드).
/// 결과는 C:\LastTruckBuild\LastTruck_날짜 폴더에 생기고, 요약은 C:\LastTruckBuild\build_result.txt에 남는다.
/// 출력 경로를 영문으로 두는 이유: 한글 경로에서 일부 빌드 단계가 문자열 변환 오류를 낸 적이 있다.
/// </summary>
public static class QuickWindowsBuild
{
    private const string Root = @"C:\LastTruckBuild";

    [MenuItem("LastTruck/Build/Windows 빌드 (C:\\LastTruckBuild)")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);
        string resultFile = Path.Combine(Root, "build_result.txt");
        string outDir = Path.Combine(Root, "LastTruck_" + DateTime.Now.ToString("MMdd_HHmm"));
        File.WriteAllText(resultFile, "started " + DateTime.Now + "\n" + outDir);

        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/MiningMapTest.unity" },
                locationPathName = Path.Combine(outDir, "LastTruck.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            string errors = "";
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) errors += m.content + "\n";
            File.WriteAllText(resultFile, "DONE result=" + s.result + " errors=" + s.totalErrors + " size=" + (s.totalSize / 1048576) + "MB time=" + s.totalTime + "\n" + outDir + "\n" + errors);
        }
        catch (Exception e)
        {
            File.WriteAllText(resultFile, "EXCEPTION " + e);
        }
    }
}
