using System.IO;
using System.Windows;

namespace DemoPolyNest;

internal class ObjHandler : IObjHandler
{
    public ObjHandler(string filePath)
    {
        obj_ = new UVObjInfo(filePath);
        reader_ = new StringReader(obj_.Info.file_content);
    }

    public string? ReadLine() => reader_.ReadLine();

    StringReader reader_;
    IList<int[]> face_list = new List<int[]>();
    IList<Vector> uv_list = new List<Vector>();
    List<int> tri_list = new List<int>();
    int line_counter = 0;

    public void Dispose() => reader_.Dispose();

    public static bool CanHandleLine(string currLine)
    {
        return currLine.StartsWith("vt ") || currLine.StartsWith("f ") ||
               currLine.StartsWith("v ") || currLine.StartsWith("vn ");
    }

    public string? ReadNext()
    {
        line_counter++;
        return reader_.ReadLine();
    }

    public enum TokenResult
    {
        Unknown,
        NoToken,
        List,
        Face
    }

    static char[] split_id = { ' ' };
    internal static TokenResult TokeniseLine(string line, out string[]? tokens)
    {
        tokens = null;
        if (string.IsNullOrEmpty(line)) return TokenResult.NoToken;

        line = line.Replace("  ", " ");
        tokens = line.Split(split_id);
        if (tokens is null || tokens.Count() < 2) return TokenResult.NoToken;
        
        if (tokens[0] == "vt") return TokenResult.List;
        else if (tokens[0] == "f") return TokenResult.Face;
        else return TokenResult.Unknown;
    }
    static char[] face_split_id = { '/' };
    public bool SurpLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;

        var result = TokeniseLine(line, out var broken);
        if (result == TokenResult.Unknown || result == TokenResult.NoToken) return false;

        if (result == TokenResult.List)
        {
            ArgumentNullException.ThrowIfNull(broken);
            uv_list.Add(new Vector(Convert.ToSingle(broken[1]), Convert.ToSingle(broken[2])));
            obj_.Info.replace_lines.Add(line_counter);
            return true;
        }
        else if (result == TokenResult.Face)
        {
            ArgumentNullException.ThrowIfNull(broken);            
            IList<int> uv_face = ConvertToFace(broken);
            int count = FaceToTris(uv_face);
            return count > 0;
        }
        else
        {
            //Debug.LogError("Should not be able to get here...");
            return false;
        }
    }

    int FaceToTris(IList<int> uv_face) // Create triangles out of the face data.
    {
        int j = 1; 
        while (j + 2 <= uv_face.Count)       
        {
            tri_list.Add(uv_face[0]);
            tri_list.Add(uv_face[j]);
            tri_list.Add(uv_face[j + 1]);

            j++;
        }
        return j; // There will generally be more than 1 triangle per face.
    }

    private IList<int> ConvertToFace(string[] broken) // give a better method name
    {
        int j = 1;
        var uv_face = new List<int>();
        while (j < broken.Length && ("" + broken[j]).Length > 0)
        {
            int[] temp = new int[3];
            string[] part = broken[j].Split(face_split_id, 3);    //Separate the face into individual components (vert, uv, normal)
            temp[0] = Convert.ToInt32(part[0]);
            if (part.Length > 1)                                  //Some .obj files skip UV and normal
            {
                if (part[1] != "")                                //Some .obj files skip the uv and not the normal
                    temp[1] = Convert.ToInt32(part[1]);
                temp[2] = Convert.ToInt32(part[2]);
            }
            j++;

            if (temp[1] > 0) uv_face.Add(temp[1] - 1);
            face_list.Add(temp);
        }
        return uv_face;
    }


    public UVObjInfo Extract()
    {
        obj_.Assign(uv_list, tri_list);
        return obj_;
    }

    UVObjInfo obj_;   

}
