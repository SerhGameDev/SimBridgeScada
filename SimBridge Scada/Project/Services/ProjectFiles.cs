using System.IO;
using System.IO.Compression;
namespace SimBridge_Scada.Project.Services;
internal static class ProjectFiles
{
    public const long MaximumArchiveBytes = 512L * 1024 * 1024;
    public static string Inside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Недопустимый путь в проекте.");
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var result=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!result.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Путь выходит за пределы проекта.");
        return result;
    }
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { write(stream); stream.Flush(true); }
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static void CopyTree(string source,string destination,bool rejectConflicts=false)
    {
        if(!Directory.Exists(source)) return;
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Ссылки на каталоги не поддерживаются в проекте.");
        Directory.CreateDirectory(destination);
        foreach(var path in Directory.EnumerateFileSystemEntries(source))
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Ссылки на файлы не поддерживаются в проекте.");
            var target=Inside(destination,Path.GetFileName(path));
            if(Directory.Exists(path)) CopyTree(path,target,rejectConflicts);
            else
            {
                if(rejectConflicts && File.Exists(target))
                {
                    using var a=File.OpenRead(path); using var b=File.OpenRead(target);
                    if(!System.Security.Cryptography.SHA256.HashData(a).SequenceEqual(System.Security.Cryptography.SHA256.HashData(b)))
                        throw new InvalidDataException($"Конфликт данных при добавлении проекта: {Path.GetFileName(path)}.");
                }
                else File.Copy(path,target,true);
            }
        }
    }
    public static void Extract(string archivePath,string destination)
    {
        using var archive=ZipFile.OpenRead(archivePath);
        if(archive.Entries.Count>10000) throw new InvalidDataException("Слишком много файлов в проекте.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
        foreach(var entry in archive.Entries)
        {
            var normalized=entry.FullName.Replace('\\','/');
            var target=Inside(destination,normalized);
            if(!names.Add(target) || ((entry.ExternalAttributes>>16)&0xF000)==0xA000) throw new InvalidDataException("Повторяющийся путь или ссылка в архиве.");
            if(entry.Length<0 || entry.Length>MaximumArchiveBytes-total) throw new InvalidDataException("Распакованный проект превышает 512 МБ.");
            total+=entry.Length;
            if(total>MaximumArchiveBytes) throw new InvalidDataException("Распакованный проект превышает 512 МБ.");
            if(normalized.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input=entry.Open(); using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write);
            var buffer=new byte[81920]; long count=0; int read;
            while((read=input.Read(buffer))>0) { count+=read; if(count>entry.Length) throw new InvalidDataException("Некорректный размер файла в архиве."); output.Write(buffer,0,read); }
        }
    }
    public static void Zip(string directory,Stream output)
    {
        var files=Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).ToArray();
        if(files.Length>10000)throw new InvalidDataException("Слишком много файлов в проекте.");
        long total=0;
        foreach(var file in files){var size=new FileInfo(file).Length;if(size>MaximumArchiveBytes-total)throw new InvalidDataException("Проект превышает 512 МБ.");total+=size;}
        using var archive=new ZipArchive(output,ZipArchiveMode.Create,true);
        foreach(var file in files)
        {
            var name=Path.GetRelativePath(directory,file).Replace('\\','/');
            archive.CreateEntryFromFile(file,name,CompressionLevel.Optimal);
        }
    }
}

