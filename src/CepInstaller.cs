using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;
interface ISettings { void Enable(); void Rollback(); }
class DebugSettings : ISettings {
 class Previous { public string Key; public object Value; public RegistryValueKind Kind; public bool Existed; }
 List<Previous> prior = new List<Previous>();
 public void Enable() { foreach(string n in new string[]{"9","10","11","12"}) {
  string path="Software\\Adobe\\CSXS."+n;
  using(RegistryKey key=Registry.CurrentUser.CreateSubKey(path)) {
   if(key==null)throw new IOException("Cannot access CEP settings.");
   object v=key.GetValue("PlayerDebugMode",null,RegistryValueOptions.DoNotExpandEnvironmentNames);
   prior.Add(new Previous{Key=path,Value=v,Existed=v!=null,Kind=v==null?RegistryValueKind.String:key.GetValueKind("PlayerDebugMode")});
   key.SetValue("PlayerDebugMode","1",RegistryValueKind.String);
  }
 } }
 public void Rollback() { for(int i=prior.Count-1;i>=0;i--)using(RegistryKey k=Registry.CurrentUser.CreateSubKey(prior[i].Key)) {
  if(prior[i].Existed)k.SetValue("PlayerDebugMode",prior[i].Value,prior[i].Kind);else k.DeleteValue("PlayerDebugMode",false);
 } }
}
class TestSettings : ISettings { public bool Enabled; public void Enable(){Enabled=true;} public void Rollback(){Enabled=false;} }
class Installer {
 public const string Bundle="com.yousufweiji.toolkit";
 public const string Version="6.5.1";
 public const string PayloadHash="49ff3714260f1f8d495c7af680735aa702a2a477a540ced17497c5983d3dcecc";
 public static byte[] Payload() { using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(MemoryStream m=new MemoryStream()){if(s==null)throw new IOException("Installation payload missing.");s.CopyTo(m);byte[] data=m.ToArray();using(SHA256 sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();if(hash!=PayloadHash)throw new IOException("Installation payload integrity check failed.");}return data;} }
 static string Full(string p){return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar);}
 public static void NoLinks(string path) { for(string p=Full(path);!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)) {if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installation paths are not supported: "+p);} }
 static void Child(string path,string root) { if(!Full(path).StartsWith(Full(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe installation path.");NoLinks(path); }
 public static int Extract(byte[] payload,string folder) {
  Directory.CreateDirectory(folder);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0;
  using(var zip=new ZipArchive(new MemoryStream(payload),ZipArchiveMode.Read))foreach(var entry in zip.Entries) {
   string name=entry.FullName.Replace('\\','/');if(!name.StartsWith(Bundle+"/",StringComparison.Ordinal)||name.IndexOf(':')>=0)throw new IOException("Unexpected package entry: "+name);
   string dest=Path.GetFullPath(Path.Combine(folder,name.Replace('/',Path.DirectorySeparatorChar)));Child(dest,folder);
   if(name.EndsWith("/")){Directory.CreateDirectory(dest);continue;}if(!seen.Add(dest))throw new IOException("Duplicate package path.");string parent=Path.GetDirectoryName(dest);if(String.IsNullOrEmpty(parent))throw new IOException("Package entry has no parent directory.");Directory.CreateDirectory(parent);
   using(Stream input=entry.Open())using(FileStream output=new FileStream(dest,FileMode.CreateNew,FileAccess.Write))input.CopyTo(output);count++;
  }
  string bundle=Path.Combine(folder,Bundle);foreach(string rel in new string[]{"CSXS/manifest.xml","client/index.html","client/main.js","client/bridge.js","client/builder-ui.js","client/sop-validator.js","jsx/sopFunctions.jsx","jsx/standalone/00_MASTER_PANEL.jsx"})if(!File.Exists(Path.Combine(bundle,rel)))throw new IOException("Missing required package file: "+rel);
  var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null};var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(Path.Combine(bundle,"CSXS/manifest.xml"),settings))doc.Load(reader);
  if(doc.DocumentElement.GetAttribute("ExtensionBundleId")!=Bundle||doc.DocumentElement.GetAttribute("ExtensionBundleVersion")!=Version)throw new IOException("Package identity/version mismatch.");
  return count;
 }
 public static string Install(string appData,ISettings settings,bool debug,Action<string> log,Func<bool> illustratorRunning,string failPoint) {
  if(illustratorRunning())throw new IOException("Close Adobe Illustrator completely, then click Install again.");
  string cep=Path.Combine(Full(appData),"Adobe","CEP"),area=Path.Combine(cep,"toolkit-install"),extensions=Path.Combine(cep,"extensions"),destination=Path.Combine(extensions,Bundle),backupRoot=Path.Combine(cep,"toolkit-backups");
  NoLinks(cep);Child(area,cep);Child(extensions,cep);Child(destination,extensions);Child(backupRoot,cep);Directory.CreateDirectory(area);
  NoLinks(Path.Combine(area,"install.lock"));
  using(var installLock=new FileStream(Path.Combine(area,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
   string job=Path.Combine(Path.GetTempPath(),"Toolkit-"+Guid.NewGuid().ToString("N")),backup=null,staged=Path.Combine(job,Bundle);bool moved=false,settingsTouched=false;NoLinks(job);
   log("Verifying and extracting embedded CEP package...");int count=Extract(Payload(),job);log("Verified "+count+" files; extension version "+Version+".");
   File.WriteAllText(Path.Combine(staged,"INSTALL_RECEIPT.txt"),"Yousufweiji Toolkit "+Version+Environment.NewLine+"Installed: "+DateTimeOffset.Now+Environment.NewLine+"Payload SHA-256: "+PayloadHash+Environment.NewLine);
   if(illustratorRunning())throw new IOException("Illustrator started during setup. Close it and retry.");
   Directory.CreateDirectory(extensions);Directory.CreateDirectory(backupRoot);
   try {
    if(debug){settingsTouched=true;settings.Enable();log("Enabled PlayerDebugMode for CSXS 9–12 (current Windows user).");}
    if(Directory.Exists(destination)){backup=Path.Combine(backupRoot,Bundle+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Child(backup,backupRoot);NoLinks(destination);Directory.Move(destination,backup);Directory.SetLastWriteTimeUtc(backup,DateTime.UtcNow);log("Previous build backed up to: "+backup);}
    if(failPoint=="after-backup")throw new IOException("Simulated failure after backup.");
    Child(destination,extensions);Directory.Move(staged,destination);moved=true;
    if(failPoint=="after-install")throw new IOException("Simulated failure after replacement.");
    try { if(Directory.Exists(job)) Directory.Delete(job,true); } catch(Exception cleanup) { log("Install completed; temporary staging cleanup deferred: "+cleanup.Message); }
    log("Installed to: "+destination);log("Open Illustrator > Window > Extensions (or Extensions Legacy) > Yousufweiji Toolkit.");
    return backup;
   } catch(Exception original) {
    var recovery=new List<string>();
    try { if(moved&&Directory.Exists(destination)){Child(destination,extensions);Child(staged,job);Directory.Move(destination,staged);}if(backup!=null&&Directory.Exists(backup)){Child(backup,backupRoot);Directory.Move(backup,destination);log("Previous build restored.");} }catch(Exception e){recovery.Add("File recovery: "+e.Message+". Backup: "+backup);}
    if(settingsTouched)try{settings.Rollback();}catch(Exception e){recovery.Add("Settings recovery: "+e.Message);}
    if(recovery.Count>0)throw new IOException(original.Message+Environment.NewLine+String.Join(Environment.NewLine,recovery));throw;
   }
  }
 }
}
