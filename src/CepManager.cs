using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Reflection;
using System.Diagnostics;
using System.Xml;
using Microsoft.Win32;

// All destinations are derived locally; API clients never supply filesystem roots.
sealed class CepManager {
 readonly string appData,profile; readonly bool test; readonly object gate=new object();
 public CepManager(string profile,bool test){this.profile=profile;this.test=test;appData=test?Path.Combine(profile,"CepSandbox"):Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);}
 string Cep {get{return Path.Combine(appData,"Adobe","CEP");}}
 string Destination {get{return Path.Combine(Cep,"extensions",Installer.Bundle);}}
 string Backups {get{return Path.Combine(Cep,"toolkit-backups");}}
 string Area {get{return Path.Combine(Cep,"toolkit-install");}}
 static string Hash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 static string VersionAt(string folder){var doc=new XmlDocument{XmlResolver=null};using(var r=XmlReader.Create(Path.Combine(folder,"CSXS","manifest.xml"),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(r);if(doc.DocumentElement.GetAttribute("ExtensionBundleId")!=Installer.Bundle)throw new IOException("Extension identity does not match Toolkit.");return doc.DocumentElement.GetAttribute("ExtensionBundleVersion");}
 bool Running(){return test?File.Exists(Path.Combine(profile,"simulate-illustrator")):Process.GetProcessesByName("Illustrator").Length>0;}
 bool DebugEnabled(){if(test)return File.Exists(Path.Combine(profile,"test-cep-debug"));foreach(string n in new[]{"9","10","11","12"})using(var k=Registry.CurrentUser.OpenSubKey("Software\\Adobe\\CSXS."+n)){if(k==null||Convert.ToString(k.GetValue("PlayerDebugMode"))!="1")return false;}return true;}
 static void Child(string p,string root){if(!Path.GetFullPath(p).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe CEP path.");Installer.NoLinks(p);}
 static IEnumerable<string> Files(string folder){Installer.NoLinks(folder);if(!Directory.Exists(folder))yield break;foreach(string f in Directory.GetFiles(folder)){Installer.NoLinks(f);yield return f;}foreach(string d in Directory.GetDirectories(folder)){Installer.NoLinks(d);foreach(string f in Files(d))yield return f;}}
 List<string> BackupNames(){var list=new List<string>();Installer.NoLinks(Backups);if(Directory.Exists(Backups)){foreach(string d in Directory.GetDirectories(Backups,Installer.Bundle+"-*")){Installer.NoLinks(d);list.Add(Path.GetFileName(d));}list.Sort((a,b)=>Directory.GetLastWriteTimeUtc(Path.Combine(Backups,b)).CompareTo(Directory.GetLastWriteTimeUtc(Path.Combine(Backups,a))));}return list;}
 public object Status(){lock(gate){try{Installer.NoLinks(Destination);var missing=new List<string>();var changed=new List<string>();var unexpected=new List<string>();string version=null,error=null;bool exists=Directory.Exists(Destination),debug=DebugEnabled();var expected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 if(exists){try{Installer.NoLinks(Path.Combine(Destination,"CSXS","manifest.xml"));version=VersionAt(Destination);}catch(Exception e){error=e.Message;}
 using(var zip=new ZipArchive(new MemoryStream(Installer.Payload()),ZipArchiveMode.Read))foreach(var entry in zip.Entries){if(entry.FullName.EndsWith("/"))continue;string rel=entry.FullName.Substring(Installer.Bundle.Length+1).Replace('/',Path.DirectorySeparatorChar);expected.Add(rel);string file=Path.Combine(Destination,rel);Child(file,Destination);if(!File.Exists(file)){missing.Add(rel);continue;}using(var a=entry.Open())using(var b=File.OpenRead(file))if(Hash(a)!=Hash(b))changed.Add(rel);}
 foreach(string file in Files(Destination)){string rel=file.Substring(Destination.Length+1);if(!expected.Contains(rel)&&rel!="INSTALL_RECEIPT.txt")unexpected.Add(rel);}}
 string state=!exists?"Not Installed":version!=null&&version!=Installer.Version?"Version mismatch":error!=null||missing.Count>0||changed.Count>0||unexpected.Count>0||!debug?"Needs repair":"Installed";
 if(File.Exists(Destination)){state="Needs repair";error="A file occupies the extension folder path. Move it manually before installing.";}
 return new {state=state,installedVersion=version,bundledVersion=Installer.Version,missing=missing,changed=changed,unexpected=unexpected,debugEnabled=debug,illustratorRunning=Running(),backups=BackupNames(),path=Destination,testMode=test,message=error??(state=="Installed"?"Bundled files and unsigned-extension settings verified. Illustrator runtime not tested by detection.":!exists?"The CEP extension is not installed for this Windows user.":"Review the file/settings differences below, then repair or restore a backup.")};
 }catch(Exception e){return new {state="Needs repair",bundledVersion=Installer.Version,message=e.Message,path=Destination,testMode=test,backups=new string[0],illustratorRunning=Running()};}}}
 sealed class FileSettings:ISettings {readonly string file;string previous;bool existed;public FileSettings(string file){this.file=file;}public void Enable(){existed=File.Exists(file);previous=existed?File.ReadAllText(file):null;File.WriteAllText(file,"enabled");}public void Rollback(){if(existed)File.WriteAllText(file,previous);else if(File.Exists(file))File.Delete(file);}}
 string NewBackup(){Directory.CreateDirectory(Backups);string b=Path.Combine(Backups,Installer.Bundle+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssffff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Child(b,Backups);return b;}
 public object Apply(string action,bool debug,string backupId){lock(gate){if(Running())throw new IOException("Close Illustrator completely before changing the CEP extension.");Installer.NoLinks(Cep);Installer.NoLinks(Destination);var log=new List<string>();string backup=null;
 if(action=="install"||action=="update"||action=="repair"){ISettings settings=test?(ISettings)new FileSettings(Path.Combine(profile,"test-cep-debug")):new DebugSettings();backup=Installer.Install(appData,settings,debug,log.Add,Running,test&&File.Exists(Path.Combine(profile,"simulate-cep-failure"))?"after-install":null);}
 else if(action=="uninstall"||action=="restore"){
 Directory.CreateDirectory(Area);Installer.NoLinks(Path.Combine(Area,"install.lock"));using(var lease=new FileStream(Path.Combine(Area,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
 if(Running())throw new IOException("Illustrator started; close it and retry.");
 if(action=="uninstall"){if(Directory.Exists(Destination)){backup=NewBackup();Directory.Move(Destination,backup);Directory.SetLastWriteTimeUtc(backup,DateTime.UtcNow);log.Add("Extension removed from Illustrator and kept as a restorable backup.");}else log.Add("Extension is already absent.");log.Add("Shared CSXS registry settings were retained.");}
 else {if(String.IsNullOrEmpty(backupId)||Path.GetFileName(backupId)!=backupId||!backupId.StartsWith(Installer.Bundle+"-",StringComparison.Ordinal))throw new IOException("Choose a listed CEP backup.");string source=Path.Combine(Backups,backupId);Child(source,Backups);if(!Directory.Exists(source))throw new IOException("That backup no longer exists.");foreach(string file in Files(source)){} VersionAt(source);Directory.CreateDirectory(Path.GetDirectoryName(Destination));if(Directory.Exists(Destination)){backup=NewBackup();Directory.Move(Destination,backup);Directory.SetLastWriteTimeUtc(backup,DateTime.UtcNow);}try{if(test&&File.Exists(Path.Combine(profile,"simulate-restore-failure")))throw new IOException("Simulated restore failure.");Directory.Move(source,Destination);}catch{if(backup!=null&&Directory.Exists(backup)&&!Directory.Exists(Destination))Directory.Move(backup,Destination);throw;}log.Add("Selected backup restored; the replaced installation was preserved.");}
 }}else throw new IOException("Unknown CEP action.");return new {status=Status(),backup=backup,log=log};}}
 public static void VerifyEmbeddedSetup(){using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("cep-setup.exe")){if(s==null||Hash(s)!="0b4638aa7364c735615d44bbcf574e48d09d9ed029602ff06ad50d0bfa0bd3cb")throw new IOException("Embedded CEP installer failed integrity verification.");}Installer.Payload();}
}
