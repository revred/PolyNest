using System.IO;
using System.Windows;

namespace DemoPolyNest;

public struct UVInfo
{
    public Vector[] uvs;
    public int[] tris;
    public string target_file;

    // do not modify
    public string file_content;         // the original file contents
    public HashSet<int> replace_lines;  // lines of the file which will be replaced on export

    public static UVInfo Create(string filepath)
    {
        UVInfo lhs = new UVInfo();

        lhs.target_file = filepath;

        lhs.replace_lines = new HashSet<int>(); //point certain lines of the file to corresponding replacement uv indices

        char[] split_id = { ' ' };
        char[] face_split_id = { '/' };

        lhs.file_content = File.ReadAllText(filepath);
        return lhs;
    }

    public UVInfo Clone()
    {

        UVInfo lhs = new UVInfo();
        lhs.uvs = new Vector[uvs.Length];
        lhs.tris = new int[tris.Length];
        lhs.replace_lines = new HashSet<int>(replace_lines);


        lhs.file_content = file_content;
        lhs.target_file = target_file;

        for (int i = 0; i < uvs.Length; i++)
            lhs.uvs[i] = uvs[i];

        for (int i = 0; i < tris.Length; i++)
            lhs.tris[i] = tris[i];

        return lhs;
    }
}
