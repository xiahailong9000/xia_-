using BestHTTP;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;




using UnityEngine.UI;
public class C_AssetManager02:MonoBehaviour {
    static C_AssetManager02 instance;
    public string o_StreamingAssetsPath,
        o_PersistenceRootPath,
        o_PersistenceSystemAssetPath,
        o_NetworkPath;
    const int o_DownLoadNumber = 5;
    const int o_CopyNumber = 5;
    const int o_ForeachLimit = 500;
    public bool o_IsDownloadComplete = false,
        o_IsDownloading = false;//Update--循环检测--
    Dictionary<string,Dictionary<string,string>>
        o_StreamingAssetConfig = new Dictionary<string,Dictionary<string,string>>(),
        o_PersistenceConfig = new Dictionary<string,Dictionary<string,string>>(),
        o_NetworkConfig = new Dictionary<string,Dictionary<string,string>>(),
        o_TempDownloadList = new Dictionary<string,Dictionary<string,string>>();
    List<string> o_TempDeleteDirctorys = new List<string>();
    List<string> o_TempDeleteFiles = new List<string>();
    int o_NetWorkVersion;
    public static C_AssetManager02 Instance {
        get {
            if (instance==null) {
                GameObject gg = new GameObject("C_AssetManager02");
                MonoBehaviour.DontDestroyOnLoad(gg);
                instance=gg.AddComponent<C_AssetManager02>();
                instance.S_init();
            }
            return instance;
        }
    }
    public Action<string,int,int> o_DownLoadState;
    public Action o_PromptIsDownLoadEvent,  //--询问是否要下载---- 
        o_DownLoadCompleteEvent; //--下载完成时间----------------
    public C_ResourceLcad o_ResourceLcad;
    void S_init() {
#if APP
        o_NetworkPath="http://192.168.1.100/Game01/SystemAssets/AndroidData";
#else
        o_NetworkPath="http://192.168.1.100/Game01/SystemAssets/PCData";
#endif        
        if (Application.platform==RuntimePlatform.WindowsEditor) {
            o_StreamingAssetsPath=Application.dataPath+"/StreamingAssets";
            string ss = Application.dataPath;
            o_PersistenceRootPath=ss.Remove(ss.Length-6,6)+"PersistenceAssets";
            o_PersistenceSystemAssetPath=o_PersistenceRootPath+"/SystemAssets";
            Debug.LogError("o_PersistencePath_________"+o_PersistenceSystemAssetPath);
        } else if (Application.platform==RuntimePlatform.Android) {
            o_StreamingAssetsPath=Application.streamingAssetsPath+"";

            string[] sss = Application.persistentDataPath.Split(new char[] { '\\','/' });
            o_PersistenceRootPath="";
            for (int i = 0;i<sss.Length-2;i++) {
                o_PersistenceRootPath+=sss[i]+'/';
            }
            o_PersistenceRootPath+="com.sycx.bundle.syste4498586";
            o_PersistenceSystemAssetPath=o_PersistenceRootPath+"/SystemAssets";
        } else if (Application.platform==RuntimePlatform.IPhonePlayer) {
            o_StreamingAssetsPath=Application.dataPath+"/Raw";
            o_PersistenceRootPath=Application.persistentDataPath;
            o_PersistenceSystemAssetPath=Application.persistentDataPath+"/SystemAssets";
        }
        if (Directory.Exists(o_PersistenceSystemAssetPath)==false) {
            Directory.CreateDirectory(o_PersistenceSystemAssetPath);
        }
        o_ResourceLcad=new C_ResourceLcad(this);
    }
    /// <summary>
    /// ------------检测刷新---------------
    /// </summary>
    public void S_CheckUpdate() {
        Debug.Log(Time.time+"________"+o_StreamingAssetsPath+"\n"+o_PersistenceSystemAssetPath+"\n"+o_NetworkPath);
        //StartCoroutine(I_WWW(o_NetworkPath+C_Tool.o_AssetsVersionName,delegate (WWW ww) {
        //    Debug.LogError(Time.time+"___WWW=============== "+C_Tool.o_AssetsVersionName+"____"+ww.text);
        //    try {
        //        o_NetWorkVersion=int.Parse(ww.text.Trim());
        S_BastHttp(o_NetworkPath+C_Tool.o_AssetsVersionName,600,delegate (byte[] zData) {
            try {
                string str = Encoding.UTF8.GetString(zData);
                o_NetWorkVersion=int.Parse(str.Trim());
                try {
                    int zLocadVersion = int.Parse(File.ReadAllText(o_PersistenceSystemAssetPath+C_Tool.o_AssetsVersionName).Trim());
                    Debug.LogError("zLocadVersion===========____"+zLocadVersion);
                    if (o_NetWorkVersion==zLocadVersion) {
                        o_IsDownloadComplete=true;
                        o_IsDownloading=false;
                        if (o_DownLoadCompleteEvent!=null) {
                            o_DownLoadCompleteEvent();
                        }
                        Debug.Log(("版本相同_______"+o_NetWorkVersion).S_Color08("00ffff"));
                    } else {
                        Debug.Log(("版本不相同_______"+o_NetWorkVersion).S_Color08("00ffff"));
                    }
                } catch (Exception ex) {
                    Debug.LogError(Time.time + "_zLocadVersion_____int.parse_失败__" + ex.StackTrace);
                }
            } catch (Exception ex) {
                Debug.LogError(Time.time+"_o_NetWorkVersion__int.parse_失败__"+ex.StackTrace);
            }
            //}));
        });
        S_WWWLcadConfig(o_NetworkPath+C_Tool.o_AssetsConfigName,delegate (Dictionary<string,Dictionary<string,string>> zNetworkConfig) {
            Debug.LogError(Time.time+"____WWW=============== "+C_Tool.o_AssetsConfigName);
            o_NetworkConfig=zNetworkConfig;
            o_PersistenceConfig=S_ReadpersistenceConfig(o_PersistenceSystemAssetPath);
            string o_StreamingAssetsPath2 = o_StreamingAssetsPath;
            if (o_StreamingAssetsPath2.Contains("file://")==false) {
                o_StreamingAssetsPath2="file://"+o_StreamingAssetsPath;
            }
            //Debug.LogError("dddddddddddddddddd");
            S_BastHttp(o_StreamingAssetsPath2+C_Tool.o_AssetsConfigName,delegate (Dictionary<string,Dictionary<string,string>> zStreamingAssetConfig) {
                o_StreamingAssetConfig=zStreamingAssetConfig;
                o_TempDownloadList.Clear();
                StartCoroutine(I_CheckUpdate());
                Debug.LogError(Time.time+"____加载完成______开始校验_______");
            });
        });
    }
    IEnumerator I_CheckUpdate() {
        yield return new WaitForSeconds(0.03f);
        int zLoopNumber = 0;
        foreach (var zNet in o_NetworkConfig) {
            Dictionary<string,string> zAddUpdate = new Dictionary<string,string>();
            if (o_PersistenceConfig.ContainsKey(zNet.Key)) {//-------沙河目录存在--------------
                Dictionary<string,string> zPersistence = o_PersistenceConfig[zNet.Key];
                foreach (var n in zNet.Value) {
                    if (zPersistence.ContainsKey(n.Key)==false) {//-沙河文件不存在----添加到下载列表
                        zAddUpdate[n.Key]=n.Value;
                    } else if (zPersistence[n.Key]!=n.Value) {//-沙河文件存在Md5不相等---添加到下载列表
                        zAddUpdate[n.Key]=n.Value;
                    }
                    o_ResourceLcad.o_IsStreamAsset[zNet.Key+"/"+n.Key]=0;
                    if (zLoopNumber>o_ForeachLimit) {
                        zLoopNumber=0;
                        //Debug.LogError("检测完成___------_400___1__");
                        yield return new WaitForSeconds(0.03f);
                    } else {
                        zLoopNumber++;
                    }
                }
            } else {//--------------沙河目录不存在------------------添加到下载列表---
                foreach (var n in zNet.Value) {
                    zAddUpdate[n.Key]=n.Value;
                    if (zLoopNumber>o_ForeachLimit) {
                        zLoopNumber=0;
                        //Debug.LogError("检测完成___------_400___2__");
                        yield return new WaitForSeconds(0.03f);
                    } else {
                        zLoopNumber++;
                    }
                }
            }
            if (o_StreamingAssetConfig.ContainsKey(zNet.Key)) {//----内部包资源目录存在----
                Dictionary<string,string> zStreaming = o_StreamingAssetConfig[zNet.Key];
                List<string> zRemoveList = new List<string>();
                foreach (var n in zAddUpdate) {//内部包资源存在且 md5相等----从下载列表移除--
                    if (zStreaming.ContainsKey(n.Key)&&zStreaming[n.Key]==n.Value) {
                        zRemoveList.Add(n.Key);
                        o_ResourceLcad.o_IsStreamAsset[zNet.Key+"/"+n.Key]=1;
                    }
                    if (zLoopNumber>o_ForeachLimit) {
                        zLoopNumber=0;
                        //Debug.LogError("检测完成___------_400___3__");
                        yield return new WaitForSeconds(0.03f);
                    } else {
                        zLoopNumber++;
                    }
                }
                for (int i = 0;i<zRemoveList.Count;i++) {
                    zAddUpdate.Remove(zRemoveList[i]);
                    if (zLoopNumber>o_ForeachLimit) {
                        zLoopNumber=0;
                        //Debug.LogError("检测完成___------_400__4___");
                        yield return new WaitForSeconds(0.03f);
                    } else {
                        zLoopNumber++;
                    }
                }
            }
            if (zAddUpdate.Count>0) {//-----将需要更新的文件添加到更新列表------------
                o_TempDownloadList[zNet.Key]=zAddUpdate;
            }
        }
        if (o_IsDownloadComplete) {
            yield break;
        }
        Dictionary<string,string> zDeleteUpdate = new Dictionary<string,string>();
        foreach (var per in o_PersistenceConfig) {//--检查需要删除的文件-------添加到文件移除列表-------
            if (o_NetworkConfig.ContainsKey(per.Key)) {
                Dictionary<string,string> zNet = o_NetworkConfig[per.Key];
                foreach (var n in per.Value) {
                    if (zNet.ContainsKey(n.Key)==false) {
                        zDeleteUpdate[n.Key]=per.Key;
                        o_TempDeleteFiles.Add(per.Key+"\\"+n.Key);
                    }
                    if (zLoopNumber>o_ForeachLimit) {
                        zLoopNumber=0;
                        //Debug.LogError("检测完成___------_400_____");
                        yield return new WaitForSeconds(0);
                    } else {
                        zLoopNumber++;
                    }
                }
            } else {//-----------------文件不存在---------添加到文件夹移除列表-------------------
                o_TempDeleteDirctorys.Add(per.Key);
            }
        }
        foreach (var n in zDeleteUpdate) {//--------多余的文件移除沙河索引------------------
            if (o_PersistenceConfig.ContainsKey(n.Value)) {
                o_PersistenceConfig[n.Value].Remove(n.Key);
            }
        }
        foreach (var n in o_TempDeleteDirctorys) {//---多余的目录移除沙河索引---------------
            o_PersistenceConfig.Remove(n);
        }
        Debug.LogError("检测完成_____mmmmmmmm_______");
        if (o_IsDownloadComplete) {
            yield break;
        }
        if (o_TempDownloadList.Count>0) {
            if (o_PromptIsDownLoadEvent==null) {
                S_DeleteResources();
                S_DownLoadNewResources();
                Debug.LogError("检测完成____-====_____");
            } else {
                S_DeleteResources();
                o_PromptIsDownLoadEvent();
                Debug.LogError("检测完成____-====___eeeeeeeeee__");
            }
            if (o_DownLoadState!=null) {
                o_DownLoadState("AssetsConfig.ab",zCurrentDownloadNumber,zDownloadTotal);
            }
        } else {
            o_IsDownloadComplete=true;
            o_IsDownloading=false;
            if (o_DownLoadCompleteEvent!=null) {
                o_DownLoadCompleteEvent();
            }
        }
    }
    void Update() {
        if (o_IsDownloading==true) {
            if (requests.Count>0) {
                if (zDownLoads.Count<5) {
                    HTTPRequest htt = requests[0];
                    requests.RemoveAt(0);
                    htt.Send();
                    zDownLoads.Add(htt);
                }
            } else {
                o_IsDownloading=false;
                if (o_DownLoadCompleteEvent!=null){
                    o_DownLoadCompleteEvent();
                }
                Screen.sleepTimeout=SleepTimeout.SystemSetting; //----防止设备休眠-----
                File.WriteAllText(o_PersistenceSystemAssetPath+C_Tool.o_AssetsVersionName,o_NetWorkVersion+"");
            }
        }
    }
    void S_DeleteResources() {
        foreach (var n in o_TempDeleteDirctorys) {
            string zfullPath = o_PersistenceSystemAssetPath+n;
            if (Directory.Exists(zfullPath)) {
                Directory.Delete(zfullPath,true);
            }
        }
        foreach (var n in o_TempDeleteFiles) {
            string zfullPath = o_PersistenceSystemAssetPath+n;
            if (Directory.Exists(zfullPath)) {
                Directory.Delete(zfullPath,true);
            }
        }
    }
    List<HTTPRequest> requests = new List<HTTPRequest>();
    List<HTTPRequest> zDownLoads = new List<HTTPRequest>();
    int zCurrentDownloadNumber,zDownloadTotal;
    public void S_DownLoadNewResources() {
        Screen.sleepTimeout=SleepTimeout.NeverSleep; //----防止设备休眠-----
        StartCoroutine(I_DownLoadNewResources());
    }
    IEnumerator I_DownLoadNewResources() {
        yield return new WaitForSeconds(0.1f);
        zCurrentDownloadNumber=0;
        zDownloadTotal=0;
        int zLoopNumber = 0;
        foreach (var mm in o_TempDownloadList) {
            foreach (var n in mm.Value) {
                if (zLoopNumber>o_ForeachLimit/2) {
                    zLoopNumber=0;
                    Debug.LogError("检测完成___------_400__4___");
                    yield return new WaitForSeconds(0.03f);
                } else {
                    zLoopNumber++;
                }
                zDownloadTotal++;
                string zMd5 = n.Value;
                string zName = mm.Key+"/"+n.Key;
                string zFullPath = o_NetworkPath+zName;
                //Debug.LogError("开始下载_____"+zFullPath);
                var request = new HTTPRequest(new Uri(zFullPath),delegate (HTTPRequest req,HTTPResponse resp) {
                    //Debug.LogError("下载_____"+req.State);
                    zDownLoads.Remove(req);
                    zCurrentDownloadNumber++;
                    if (o_DownLoadState!=null) {
                        o_DownLoadState(zName,zCurrentDownloadNumber,zDownloadTotal);
                    }
                    switch (req.State) {
                        case HTTPRequestStates.Finished:
                            if (resp.IsSuccess) {
                                string[] zNames = req.Tag.ToString().Split('*');
                                //Debug.LogError("下载成功_____"+req.CurrentUri.LocalPath);
                                byte[] data = resp.Data;
                                string fullPath = o_PersistenceSystemAssetPath+zNames[0]+"\\"+zNames[1];
                                fullPath=fullPath.Replace("\\","/");
                                string zDirectory = Path.GetDirectoryName(fullPath);
                                if (Directory.Exists(zDirectory)==false) Directory.CreateDirectory(zDirectory);
                                if (File.Exists(fullPath)) File.Delete(fullPath);
                                Debug.LogError("文件保存____"+fullPath);
                                using (FileStream fs = File.Open(fullPath,FileMode.Create,FileAccess.Write,FileShare.None)) {
                                    fs.Write(data,0,data.Length);
                                    fs.Flush();
                                    fs.Close();
                                    data=null;
                                }
                                S_SavePersistenceConfig(zNames[0],zNames[1],zMd5);
                            } else {
                            };
                            break;
                    }
                    req.Clear();
                    resp.Dispose();
                });
                request.Tag=mm.Key+"*"+n.Key;
                request.DisableCache=true;
                request.Timeout=TimeSpan.FromSeconds(600);
                //request.Send();
                requests.Add(request);
                if (requests.Count>0) {
                    o_IsDownloadComplete=false;
                    o_IsDownloading=true;
                }
            }
        }
        if (o_TempDownloadList.Count==0) {
            if (o_DownLoadCompleteEvent!=null) {
                o_DownLoadCompleteEvent();
            }
        }
    }
    void S_SavePersistenceConfig(string zPath,string zName,string zMd5) {
        if (o_PersistenceConfig.ContainsKey(zPath)==false) {
            o_PersistenceConfig[zPath]=new Dictionary<string,string>();
        }
        o_PersistenceConfig[zPath][zName]=zMd5;
        Dictionary<string,string> mmm = o_PersistenceConfig[zPath];
        StringBuilder ssb = new StringBuilder();
        foreach (var n in mmm) {
            ssb.Append(n.Value+"\t"+n.Key+"\r\n");
            //Debug.Log((n.Value+"________________"+n.Key).S_Color08("00ff99"));
        }
        string zFullPath = o_PersistenceSystemAssetPath+zPath+C_Tool.o_AssetConfigName;
        //Debug.LogError("ddddd____"+zFullPath+"__________"+ssb.ToString());
        //File.WriteAllText(zFullPath,ssb.ToString(),Encoding.UTF8);
        byte[] data = Encoding.UTF8.GetBytes(ssb.ToString());
        if (File.Exists(zFullPath)) File.Delete(zFullPath);
        using (FileStream fs = File.Open(zFullPath,FileMode.Create,FileAccess.Write,FileShare.None)) {
            fs.Write(data,0,data.Length);
            fs.Flush();
            // fs.Close();
            data=null;
        }
    }
    //检查-校验----
    void S_WWWLcadConfig(string zPath,Action<Dictionary<string,Dictionary<string,string>>> zEndEvene) {
        Debug.LogError("S_LcadConfig________"+zPath);
        StartCoroutine(I_WWW(zPath,delegate (WWW ww) {
            Dictionary<string,Dictionary<string,string>> zConfig = new Dictionary<string,Dictionary<string,string>>();
            if (ww!=null) {
                zConfig=S_ReadAssetsConfig(ww.text);
            }
            if (zEndEvene!=null) {
                zEndEvene(zConfig);
            }
        }));
    }
    void S_BastHttp(string zPath,Action<Dictionary<string,Dictionary<string,string>>> zEndEvene) { 
        S_BastHttp(zPath,1000,delegate (byte[] zData) {      
            Dictionary<string,Dictionary<string,string>> zConfig = new Dictionary<string,Dictionary<string,string>>();
            if (zData!=null) {
                string str = Encoding.UTF8.GetString(zData);
                zConfig=S_ReadAssetsConfig(str);
            }
            if (zEndEvene!=null) {
                zEndEvene(zConfig);
            }
        });
    }
    void S_BastHttp(string zFullPath,int zTimeout,Action<byte[]> zEndEvene) {
        var request = new HTTPRequest(new Uri(zFullPath),delegate (HTTPRequest req,HTTPResponse resp) {
            switch (req.State) {
                case HTTPRequestStates.Finished:
                    if (resp.IsSuccess) {
                        Debug.LogError("BastHttp读取成功___"+zFullPath);
                        if (zEndEvene!=null) {
                            zEndEvene(resp.Data);
                        }
                    };
                    return;
            }
            Debug.LogError("BastHttp读取失败___"+zFullPath);
            if (zEndEvene!=null) {
                zEndEvene(null);
            }
        }); 
        request.Tag=1;
        request.DisableCache=true;
        request.Timeout=TimeSpan.FromSeconds(zTimeout);
        request.Send();
    }
    IEnumerator I_WWW(string zPath,Action<WWW> zEndEvene) {
        WWW ww = new WWW(zPath);
        //Debug.Log("开始WWWDownlcad__"+zPath.S_Color08("559988"));
        yield return ww;
        if (ww.error==null) {
            if (zEndEvene!=null) {
                zEndEvene(ww);
            }
        } else {
            Debug.Log(("www出错_"+zPath+"___"+ww.error).S_Color08("FF9900"));
            if (zEndEvene!=null) {
                zEndEvene(null);
            }
        }
    }
    public static Dictionary<string,Dictionary<string,string>> S_ReadpersistenceConfig(string zLcadDictionary = null) {
        if (string.IsNullOrEmpty(zLcadDictionary)) {
            zLcadDictionary=System.Environment.CurrentDirectory;
        }
        Console.WriteLine(zLcadDictionary);
        string zLcadDictionary0 = zLcadDictionary+C_Tool.o_AssetConfigName;
        Dictionary<string,Dictionary<string,string>> zPersi = new Dictionary<string,Dictionary<string,string>>();
        try {
            string ss = File.ReadAllText(zLcadDictionary0);
            Dictionary<string,string> zDic = S_ReadConfig(ss);
            if (zDic.Count>0) {
                zPersi[""]=zDic;
            }
        } catch { }
        //Debug.LogError("获取沙河Config__zLcadDictionary___________"+zLcadDictionary);
        string[] zDirectorys = Directory.GetDirectories(zLcadDictionary,"*",SearchOption.AllDirectories);
        int iiy = zLcadDictionary.Length;
        for (int i = 0;i<zDirectorys.Length;i++) {
            //Debug.LogError((zDirectorys[i]+"_____________kkk___________"+zLcadDictionary).S_Color08("00ff00"));
            try {
                string zLcadDictionary1 = zDirectorys[i]+C_Tool.o_AssetConfigName;
                string ss = File.ReadAllText(zLcadDictionary1);
                Dictionary<string,string> zDic = S_ReadConfig(ss);
                if (zDic.Count>0) {
                    string ssq = zDirectorys[i].Remove(0,iiy).Replace("\\","/");
                    zPersi[ssq]=zDic;
                }
            } catch { }
        }
        //foreach(var mm2 in zPersi) {
        //    Debug.LogError((mm2.Key+"========================================"+zLcadDictionary).S_Color08("00ff99"));
        //    foreach (var n in mm2.Value) {
        //        Debug.LogError(n.Key+"________________________"+n.Value);
        //    }
        //}
        return zPersi;
    }
    public static Dictionary<string,Dictionary<string,string>> S_ReadAssetsConfig(string ss) {
        Dictionary<string,Dictionary<string,string>> zConfig = new Dictionary<string,Dictionary<string,string>>();
        string[] sss0 = ss.Split('?');
        for (int x = 1;x<sss0.Length;x++) {
            string[] sss8 = sss0[x].Split('*');
            Dictionary<string,string> zDic = S_ReadConfig(sss8[1]);
            if (zDic.Count>0) {
                zConfig[sss8[0].Trim()]=zDic;
            }
        }
        return zConfig;
    }
    static Dictionary<string,string> S_ReadConfig(string ss) {
        Dictionary<string,string> zDic = new Dictionary<string,string>();
        string[] sss = ss.Split(new string[] { "\r\n" },StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0;i<sss.Length;i++) {
            if (sss[i].Length>5) {
                string[] sss3 = sss[i].Split('\t');
                //Debug.LogWarning(sss0[0]+"___________"+sss0[1]);
                zDic.Add(sss3[1].Trim(),sss3[0].Trim());
            }
        }
        return zDic;
    }
    void OnApplicationQuit() {
        // BestHTTP.HTTPUpdateDelegator.CheckInstance();
        HTTPManager.OnQuit();
        //isAppcationQuit=true;
        if (requests.Count>0) {
            for (int i = requests.Count-1;i>=0;i--) {
                HTTPRequest rq = requests[i];
                if (rq!=null) {
                    rq.Abort();
                }
            }
            requests.Clear();
        }
    }
    //string fullPath = @"\WebSite1\Default.aspx";
    //System.IO.Path.GetFileName(fullPath);//文件名  “Default.aspx”
    //System.IO.Path.GetExtension(fullPath);//扩展名 “.aspx”
    //System.IO.Path.GetFileNameWithoutExtension(fullPath);// 没有扩展名的文件名 “Default”
    public class C_Tool {
        public static string
            o_AssetsConfigName = "/AssetsConfig.mxen",
            o_AssetsVersionName = "/AssetsVersion.mxen",
            o_AssetConfigName = "/AssetConfig.mxen";
        //static List<string> zTypes = new List<string> { ".ab",".txt",".png",".jpg",".manifest" };
        static List<string> zTypes = new List<string> { ".ab",".txt",".png",".jpg"};
        public static void S_CreatAssetsConfigExe(string zLcadDictionary = null) {
            if (string.IsNullOrEmpty(zLcadDictionary)) {
                zLcadDictionary=System.Environment.CurrentDirectory;
            }
            string zConfigSavePath= zLcadDictionary+o_AssetsConfigName;
            string zVersionSavePath = zLcadDictionary+o_AssetsVersionName;
            if (File.Exists(zConfigSavePath)) {
                File.Delete(zConfigSavePath);
            }
            if (File.Exists(zVersionSavePath)) {
                File.Delete(zVersionSavePath);
            }
            Dictionary<string,Dictionary<string,string>> zDic = new Dictionary<string,Dictionary<string,string>>();
            S_GetAllFile(new DirectoryInfo(zLcadDictionary),zLcadDictionary.Length,zDic);
            StringBuilder ssb = new StringBuilder();
            foreach (var n in zDic) {
                Console.WriteLine(n.Key);
                ssb.Append("?"+n.Key+"*\r\n");
                foreach (var nn in n.Value) {
                    ssb.Append(nn.Value+"\t"+nn.Key+"\r\n");
                }
            }
            File.WriteAllText(zConfigSavePath,ssb.ToString(),Encoding.UTF8);
            File.WriteAllText(zVersionSavePath,S_VersionTime(),Encoding.UTF8);
        }
        static void S_GetAllFile(DirectoryInfo zdir,int zLength,Dictionary<string,Dictionary<string,string>> zDic) {
            Console.WriteLine("检测目录_____________"+zdir.FullName);
            FileInfo[] files = zdir.GetFiles();
            if (files.Length>0) {
                string ss0 = zdir.FullName.Remove(0,zLength).Replace("\\","/");
                Dictionary<string,string> zss = new Dictionary<string,string>();
                foreach (FileInfo file in files) {
                    //Console.WriteLine(file.Name+"___"+file.Extension);
                    if (zTypes.Contains(file.Extension)) {
                        //Console.WriteLine(file.FullName);
                        string zMd5 = GetMD5HashFromFile(file.FullName);
                        zss[file.Name]=zMd5;
                    }
                }
                zDic[ss0]=zss;
            }
            DirectoryInfo[] dis = zdir.GetDirectories();
            foreach (DirectoryInfo xdi in dis) {
                S_GetAllFile(xdi,zLength,zDic);//这里是递归
            }
        }
        static string GetMD5HashFromFile(string fileName) {
            try {
                FileStream file = new FileStream(fileName,FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();
                StringBuilder sb = new StringBuilder();
                for (int i = 0;i<retVal.Length;i++) {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            } catch (Exception ex) {
                throw new Exception("GetMD5HashFromFile() fail,error:"+ex.Message);
            }
        }
        public static string S_VersionTime() {
           return DateTime.Now.ToString("yyMMddHHmm");
        }
    }
    public class C_ResourceLcad {
        C_AssetManager02 o_Root;
        public Dictionary<string,int> o_IsStreamAsset = new Dictionary<string,int>();//路径类型--
        public C_ResourceLcad(C_AssetManager02 zRoot) {
            o_Root=zRoot;
        }
        public string S_AssetsPath(string ss) {
            if (o_IsStreamAsset.ContainsKey(ss)) {
                if (o_IsStreamAsset[ss]==1) {
                    return o_Root.o_StreamingAssetsPath+ss;
                } else {
                    return o_Root.o_PersistenceSystemAssetPath+ss; 
                }
            } else {
                string zper= o_Root.o_PersistenceSystemAssetPath+ss;
                if (File.Exists(zper)) {
                    o_IsStreamAsset[ss]=0;
                } else {
                    o_IsStreamAsset[ss]=1;
                }
               return S_AssetsPath(ss);
            }
        }
        Dictionary<string,UnityEngine.Object> assetMap = new Dictionary<string,UnityEngine.Object>();
        public void S_LoadAssetAsync<T>(string ab_path,Action<T> zEndEvent) where T : UnityEngine.Object {
            string ab_path2 = C_Tools.S_消除中文字符(ab_path);
            string fullpath = S_AssetsPath(ab_path2);
            //Debug.Log(ab_path + "___异步开始_AssetBunde_____" + fullpath);
            if (assetMap.ContainsKey(ab_path)) {
                o_Root.S_Delayed(0).o_Event=delegate () {
                    Debug.Log(ab_path+"___异步_有现成存货_____"+fullpath);
                    T asset = assetMap[ab_path] as T;
                    if (zEndEvent!=null) {
                        zEndEvent(asset);
                    }
                };
            } else {
                o_Root.StartCoroutine(I_LcadAssetAsync(fullpath,delegate (AssetBundle bundle) {
                    try {
                        T asset = bundle.LoadAllAssets<T>()[0];
                        bundle.Unload(false);
                        assetMap[ab_path]=asset;
                        //Debug.Log(ab_path + "___异步_AssetBunde加载成功_____" + fullpath);
                        if (zEndEvent!=null) {
                            zEndEvent(asset);
                        }
                    } catch (Exception ex) {
                        Debug.LogError(ab_path+"_ 出错_____"+fullpath+"____"+ex.Message);
                        if (zEndEvent!=null) {
                            zEndEvent(null);
                        }
                    }
                }));
            }
        }
        IEnumerator I_LcadAssetAsync(string fullpath,Action<AssetBundle> zEndEvent) {
            AssetBundleCreateRequest req = AssetBundle.LoadFromFileAsync(fullpath);
            //Debug.LogError("协调__-----------------____"+fullpath);
            yield return req;
            if (req.isDone) {
                zEndEvent(req.assetBundle);
            } else {
                Debug.LogError("_失败__AssetBunde__Async____"+fullpath);
                if (zEndEvent!=null) {
                    zEndEvent(null);
                }
            }
        }
        public void S_LoadSceneAsync(string zPath,string name,Action<Scene> zEndEvent) {
            zPath=C_Tools.S_消除中文字符(zPath);
            name=C_Tools.S_消除中文字符(name);
            string fullpath = S_AssetsPath(zPath);
            o_Root.StartCoroutine(I_LoadSceneAsync(fullpath,name,zEndEvent));
            return;
        }
        IEnumerator I_LoadSceneAsync(string fullpath,string name,Action<Scene> zEndEvent) {
            AssetBundleCreateRequest abcr = AssetBundle.LoadFromFileAsync(fullpath);
            yield return abcr;
            //Debug.Log("<Color=#88FF00>加载场景_===============__"+name+"____</Color>__"+fullpath);
            AsyncOperation zAsync = SceneManager.LoadSceneAsync(name);
            yield return zAsync;
            Scene zScene = SceneManager.GetSceneByName(name);
            //Debug.Log("<Color=#88FF00>加载场景___"+name+"____</Color>__"+fullpath);
            if (zEndEvent!=null) {
                zEndEvent(zScene);
            }
        }
    }
    public class C_Zip:MonoBehaviour {
        ZipFile zf;
        int index;
        public string  o_ZipReadPath,o_ZipSaveDirectory;
        DateTime o_ZipCreateTime;
        public Action<string,int,long> d_ZipInfo;
        public static C_Zip S_Get() {
            GameObject gg = new GameObject("zip");
            MonoBehaviour.DontDestroyOnLoad(gg);
            C_Zip nn= gg.AddComponent<C_Zip>();
            return nn;
        }
        void Awake() {
            //o_ZipReadPath=Application.streamingAssetsPath+"/assets.zip";
            //o_ZipReadPath = Path.Combine(Application.temporaryCachePath,"assets.zip");
        }
        void Start() {
            if (File.Exists(o_ZipReadPath)) {
                o_ZipCreateTime=File.GetLastWriteTime(o_ZipReadPath);
                FileStream fs = File.OpenRead(o_ZipReadPath);
                zf=new ZipFile(fs);
                index=0;
            } else {
                Debug.LogError("assets.zip 不存在");
            }
        }
        void Update() {
            if (index<zf.Count) {
                ZipEntry zipEntry = zf[index];
                index++;
                if (d_ZipInfo!=null) {
                    d_ZipInfo(zipEntry.Name,index,zf.Count);
                }
                if (!zipEntry.IsFile) {
                    return; 
                }
                byte[] buffer = new byte[4096*2048*10]; 
                Stream zipStream = zf.GetInputStream(zipEntry);
                string zZipFullPath = o_ZipSaveDirectory+"/"+zipEntry.Name;
                string zDirectory = Path.GetDirectoryName(zZipFullPath);
                if (zDirectory.Length>0) {
                    Directory.CreateDirectory(zDirectory);
                }
                using (FileStream streamWriter = File.Create(zZipFullPath)) {
                    StreamUtils.Copy(zipStream,streamWriter,buffer);
                }
            }
        }
        void OnDestroy() {
            if (zf!=null) {
                zf.IsStreamOwner=true; 
                zf.Close(); 
            }
        }
        public static void S_CreateZip(string zSaveFullPath,string password,string zZipPath) {
            zSaveFullPath=zSaveFullPath.Replace("\\","/");
            FileStream fsOut = File.Create(zSaveFullPath);
            ZipOutputStream zipStream = new ZipOutputStream(fsOut);
            zipStream.SetLevel(3); 
            int folderOffset = zZipPath.Length+(zZipPath.EndsWith("\\") ? 0 : 1);
            S_CompressFolder(zZipPath,zipStream,folderOffset);
            zipStream.IsStreamOwner=true;   
            zipStream.Close();
        }
        static void S_CompressFolder(string zZipPath,ZipOutputStream zipStream,int folderOffset) {
            string[] files = Directory.GetFiles(zZipPath,"*.*");
            foreach (string filename in files) {
                FileInfo fi = new FileInfo(filename);
                string entryName = filename.Substring(folderOffset);
                Debug.LogError(filename+"_________"+entryName);
                entryName=ZipEntry.CleanName(entryName); 
                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.IsUnicodeText=true;
                newEntry.DateTime=fi.LastWriteTime; 
                newEntry.Size=fi.Length;
                zipStream.PutNextEntry(newEntry);
                byte[] buffer = new byte[4096*2024*10];
                using (FileStream streamReader = File.OpenRead(filename)) {
                    StreamUtils.Copy(streamReader,zipStream,buffer);
                }
                zipStream.CloseEntry();
            }
            string[] folders = Directory.GetDirectories(zZipPath);
            foreach (string folder in folders) {
                S_CompressFolder(folder,zipStream,folderOffset);
            }
        }
    }
}
public static class C_Tool_jsdkjsksas0 {
    public static string S_Color08(this string ss,string z颜色) {
        return string.Format("<color=#{0}>{1}</color>",z颜色,ss);
    }
}
public class C_Demo_045:MonoBehaviour {
    public C_SystemPromptView2 o_SystemPromptView;
    void Start() {
        C_AssetManager02.Instance.S_CheckUpdate();
        o_SystemPromptView.S_Init();
    }
    [Serializable]
    public class C_SystemPromptView2 {
        public GameObject o_Root;
        public Text o_Content;
        public Button o_Cancel, o_Ok;
        Action<bool> o_OkEvent;
        public void S_Init() {
            o_Root.SetActive(false);
            o_Cancel.onClick.AddListener(delegate () {
                o_Root.SetActive(false);
                if (o_OkEvent!=null) {
                    o_OkEvent(false);
                }
            });
            o_Ok.onClick.AddListener(delegate () {
                o_Root.SetActive(false);
                if (o_OkEvent!=null) {
                    o_OkEvent(true);
                }
            });
        }
        public void S_ShowPrompt(string zContent,Action<bool> zPrompt) {
            o_Root.SetActive(true);
            o_Content.text=zContent;
            o_OkEvent=zPrompt;
        }
    }
}

