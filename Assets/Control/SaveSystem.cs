using System.IO;
using System;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Collections.Generic;

public static class SaveSystem
{
    private static readonly string path = Path.Combine(Application.persistentDataPath, "database.json");
    private static readonly string tmpPath = Path.Combine(Application.persistentDataPath, "database.json.tmp");
    private static readonly string bakPath = Path.Combine(Application.persistentDataPath, "database.json.bak");
    private static readonly string bak2Path = Path.Combine(Application.persistentDataPath, "database.json.bak2");

    private class PrivateFieldsContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            if (!prop.Writable)
            {
                var field = member as FieldInfo;
                if (field != null) prop.Writable = true;
            }
            return prop;
        }

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var members = new List<MemberInfo>();
            members.AddRange(objectType.GetFields(flags));
            members.AddRange(objectType.GetProperties(flags)); 
            return members;
        }
    }

    private static readonly JsonSerializerSettings settings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.All,
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new PrivateFieldsContractResolver()
    };

    // =========================
    // SAVE (Sicherer Modus)
    // =========================

    public static void SaveDatabase(Database db)
    {
        if (db == null) return;

        try
        {
            // 1. Serialisieren in den Speicher
            string json = JsonConvert.SerializeObject(db, settings);

            // 2. In Temp-Datei schreiben
            File.WriteAllText(tmpPath, json);

            // 3. Validierung: Ist die geschriebene Datei valide?
            if (IsFileValid(tmpPath))
            {
                // Erst wenn die Temp-Datei okay ist, rotieren wir die Backups
                RotateBackups();
                // Temp-Datei wird zur Haupt-Datei
                ReplaceFile(tmpPath, path);
                Debug.Log("<color=green>Datenbank erfolgreich gesichert.</color>");
            }
            else
            {
                throw new Exception("Temp-Datei Validierung fehlgeschlagen (Datei leer oder korrupt).");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("SCHWERER FEHLER beim Speichern: " + e.Message);
        }
    }

    // =========================
    // LOAD (Mit Fallback-Kette)
    // =========================

    public static Database LoadDatabase()
    {
        // 1. Prüfen, ob überhaupt irgendeine Datei existiert (sonst ist es ein Erststart)
        if (!File.Exists(path) && !File.Exists(bakPath) && !File.Exists(bak2Path))
        {
            Debug.Log("<color=cyan>Erststart erkannt: Keine Datenbank vorhanden. Erstelle neues Profil.</color>");
            return new Database();
        }

        // 2. Versuche nacheinander zu laden
        if (TryLoadAtPath(path, out var db)) return db;
        
        Debug.LogWarning("Haupt-Datenbank korrupt, versuche Backup 1...");
        if (TryLoadAtPath(bakPath, out db)) return db;
        
        Debug.LogWarning("Backup 1 korrupt, versuche Backup 2...");
        if (TryLoadAtPath(bak2Path, out db)) return db;

        // 3. Nur wenn Dateien DA waren, aber alle kaputt sind, kommt dieser Error:
        Debug.LogError("Datenbank-Dateien existieren, sind aber alle korrupt! Notfall-Reset.");
        return new Database();
    }


    private static bool TryLoadAtPath(string p, out Database db)
    {
        db = null;
        if (!File.Exists(p) || new FileInfo(p).Length == 0) return false;

        try
        {
            string json = File.ReadAllText(p);
            db = JsonConvert.DeserializeObject<Database>(json, settings);
            
            if (db == null) return false;

            // ⭐ HIER: Repair-Step vor allem anderen
            ObjectRepairer.Repair(db);

            db.InitializeAfterLoad();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Validierungsfehler bei {Path.GetFileName(p)}: {e.Message}");
            TryMarkCorrupt(p);
            return false;
        }
    }

    // =========================
    // HELFER
    // =========================

    private static bool IsFileValid(string p)
    {
        if (!File.Exists(p)) return false;
        long length = new FileInfo(p).Length;
        // Eine valide Datenbank wird in der Regel mehr als 10 Bytes haben (JSON-Klammern etc.)
        return length > 5; 
    }

    private static void RotateBackups()
    {
        try 
        {
            // Schiebe bak -> bak2 (nur wenn bak existiert und valide ist)
            if (IsFileValid(bakPath)) 
                ReplaceFile(bakPath, bak2Path);

            // Schiebe main -> bak (nur wenn main existiert und valide ist)
            if (IsFileValid(path)) 
                ReplaceFile(path, bakPath);
        }
        catch (Exception e) { Debug.LogWarning("Backup-Rotation fehlgeschlagen: " + e.Message); }
    }

    private static void ReplaceFile(string src, string dst)
    {
        if (!File.Exists(src)) return;
        
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
    }

    private static void TryMarkCorrupt(string p)
    {
        try
        {
            if (!File.Exists(p)) return;
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = Path.GetFileNameWithoutExtension(p);
            string corruptPath = Path.Combine(Application.persistentDataPath, $"{fileName}_corrupt_{ts}.json");
            
            // Verschieben statt Kopieren, damit die kaputte Datei nicht den Ladevorgang blockiert
            File.Move(p, corruptPath);
            Debug.LogWarning($"Korrupte Datei wurde isoliert: {corruptPath}");
        }
        catch (Exception e) { Debug.LogError("Isolierung korrupter Datei fehlgeschlagen: " + e.Message); }
    }
}
