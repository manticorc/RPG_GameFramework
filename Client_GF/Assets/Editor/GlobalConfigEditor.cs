using System.IO;
using UnityEditor;
using UnityEngine;

public class GlobalProtoEditor : EditorWindow {
    const string path = @"./Assets/Editor/Config/GlobalProto.txt";

    private GlobalProto globalProto;

    //[MenuItem("Dark God/ȫ������")]
    public static void ShowWindow() {
        GetWindow<GlobalProtoEditor>();
    }
    //[MenuItem("Dark God/web��Դ������")]//ET����Դ�����������ﲻ���ã���֪Ϊʲô
    public static void OpenFileServer() {
        ProcessHelper.Run("dotnet", "FileServer.dll", "../FileServer/");
    }
    public void Awake() {
        if (File.Exists(path)) {
            this.globalProto = JsonHelper.FromJson<GlobalProto>(File.ReadAllText(path));
        }
        else {
            this.globalProto = new GlobalProto();
        }
    }

    public void OnGUI() {
        if (globalProto == null) return;
        this.globalProto.AssetBundleServerUrl = EditorGUILayout.TextField("��Դ·��:", this.globalProto.AssetBundleServerUrl);
        this.globalProto.Address = EditorGUILayout.TextField("��������ַ:", this.globalProto.Address);

        if (GUILayout.Button("����")) {
            File.WriteAllText(path, JsonHelper.ToJson(this.globalProto));
            AssetDatabase.Refresh();
        }
    }
}
