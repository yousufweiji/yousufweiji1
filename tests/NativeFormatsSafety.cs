using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

class NativeFormatsSafety {
 static readonly List<string> Checks = new List<string>();
 static void Pass(bool condition, string name) {
  if (!condition) throw new Exception(name);
  Checks.Add(name);
  Console.WriteLine("PASS " + name);
 }
 static string B64(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)); }
 static int Main(string[] args) {
  try {
   bool failed = false;
   try { NativeFormats.Decode("not-base64"); } catch (FormatException) { failed = true; }
   Pass(failed, "Invalid image base64 is rejected");

   failed = false;
   try { NativeFormats.ReadWorkbook(B64("not-a-zip")); } catch (Exception) { failed = true; }
   Pass(failed, "Malformed workbook archive is rejected");

   failed = false;
   try {
    string xml = "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///secret'>]><x>&e;</x>";
    NativeFormats.ReadWorkbook(Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)));
   } catch (Exception) { failed = true; }
   Pass(failed, "Non-ZIP XML input cannot reach workbook parsing");

   File.WriteAllText(args[0], new JavaScriptSerializer().Serialize(new { passed = true, checks = Checks }));
   return 0;
  } catch (Exception e) {
   Console.Error.WriteLine(e);
   return 1;
  }
 }
}
