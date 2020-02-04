/*   This file is part of CsvToPly.
*
*    CsvToPly is free software: you can redistribute it and/or modify
*    it under the terms of the GNU General Public License as published by
*    the Free Software Foundation, either version 3 of the License, or
*    (at your option) any later version.
*
*    CsvToPly is distributed in the hope that it will be useful,
*    but WITHOUT ANY WARRANTY; without even the implied warranty of
*    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*    GNU General Public License for more details.
*
*    You should have received a copy of the GNU General Public License
*    along with CsvToPly.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using LumenWorks.Framework.IO.Csv;

namespace CsvToPly
{
    internal class Program
    {
        struct RGBA
        {
            public int R;
            public int G;
            public int B;
            public int A;
        }

        struct Vertex
        {
            public int Index;
            public float X;
            public float Y;
            public float Z;
            public float NX;
            public float NY;
            public float NZ;
            public float U;
            public float V;
            public RGBA Color;
        }

        static int GetIndex(string str, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (str.Equals(headers[i], StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        static void RequiredIndex(string str, string[] headers, out int index)
        {
            if((index = GetIndex(str,headers)) == -1) throw new Exception($"Required field {str} not present");
        }

        //Allows for numbers marked 0x in hex (int)
        //Sets invariant culture
        static T Parse<T>(string mystring)
        {
            var foo = TypeDescriptor.GetConverter(typeof(T));
            return (T)(foo.ConvertFromInvariantString(mystring));
        }

        private const string RGX =
            @".*\(\s*([0-9a-fA-FxX]*)\s*,\s*([0-9a-fA-FxX]*)\s*,\s*([0-9a-fA-FxX]*)\s*,\s*([0-9a-fA-FxX]*)\s*\)";
        static Regex diffuseRegex = new Regex(RGX);
        static RGBA ParseRGBA(string s)
        {
            var m = diffuseRegex.Match(s);
            if (m.Success)
            {
                var rgba = new RGBA();
                rgba.R = Parse<int>(m.Groups[2].Value);
                rgba.G = Parse<int>(m.Groups[3].Value);
                rgba.B = Parse<int>(m.Groups[4].Value);
                rgba.A = Parse<int>(m.Groups[1].Value);
                return rgba;
            }
            throw new Exception("Failed to parse Diffuse");
        } 

        static void ShowHelp (Mono.Options.OptionSet p)
        {
            Console.WriteLine ("Usage: CsvToPly [OPTIONS]+ input.csv output.ply");
            Console.WriteLine("Converts a mesh CSV from PIX to a PLY mesh");
            Console.WriteLine ();
            Console.WriteLine ("Options:");
            p.WriteOptionDescriptions (Console.Out);
        }
        
        public static void Main(string[] args)
        {
            float offsetX = 0;
            float offsetY = 0;
            float offsetZ = 0;
            bool showHelp = false;
            bool yUp = false;
            bool flipUvs = false;
            var opts = new Mono.Options.OptionSet()
            {
                {"ox|offsetx=", "Offset to add to Position[0]", s => offsetX = Parse<float>(s)},
                {"oy|offsety=", "Offset to add to Position[1]", s => offsetY = Parse<float>(s)},
                {"oz|offsetz=", "Offset to add to Position[2]", s => offsetZ = Parse<float>(s)},
                {"f|flipuv", "Flip UVs", s => flipUvs = s != null},
                {"yup", "Don't translate coordinates to Z Up", s => yUp = s != null},
                {"h|help", "Show this message and exit", s => showHelp = s != null}
            };
            
            List<string> extra;
            try {
                extra = opts.Parse (args);
            }
            catch (Mono.Options.OptionException e) {
                Console.Write ("CsvToPly: ");
                Console.WriteLine (e.Message);
                Console.WriteLine ("Try `CsvToPly --help' for more information.");
                return;
            }
            if (extra.Count < 2) showHelp = true;
            if (showHelp)
            {
                ShowHelp(opts);
                return;
            }
            List<Vertex> vertices = new List<Vertex>();
            List<int> indices = new List<int>();
            int indexIDX;
            int indexX;
            int indexY;
            int indexZ;
            int indexNX;
            int indexNY;
            int indexNZ;
            int indexDiffuse;
            int indexU;
            int indexV;
            
            using (var csv = new CachedCsvReader(new StreamReader(extra[0], true)))
            {
                var headers = csv.GetFieldHeaders();
                RequiredIndex("IDX", headers, out indexIDX);
                RequiredIndex("Position[0]", headers, out indexX);
                RequiredIndex("Position[1]", headers, out indexY);
                RequiredIndex("Position[2]", headers, out indexZ);
                indexDiffuse = GetIndex("Diffuse", headers);
                indexU = GetIndex("Texcoord0[0]", headers);
                indexV = GetIndex("Texcoord0[1]", headers);
                indexNX = GetIndex("Normal[0]", headers);
                indexNY = GetIndex("Normal[1]", headers);
                indexNZ = GetIndex("Normal[2]", headers);
                if (GetIndex("Texcoord1[0]", headers) != -1) {
                    Console.Error.WriteLine("Warning: Input has multiple UV maps, only outputting Texcoord0");
                }
                
                foreach (var row in csv)
                {
                    int index = int.Parse(row[indexIDX]);
                    indices.Add(index);
                    bool contains = false;
                    foreach (var vtx in vertices) {
                        if (vtx.Index == index)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (contains) continue;
                    var vert = new Vertex();
                    vert.Index = index;
                    vert.X = Parse<float>(row[indexX]) + offsetX;
                    vert.Y = Parse<float>(row[indexY]) + offsetY;
                    vert.Z = Parse<float>(row[indexZ]) + offsetZ;
                    if (indexU != -1) vert.U = Parse<float>(row[indexU]);
                    if (indexV != -1) vert.V = Parse<float>(row[indexV]);
                    if (indexDiffuse != -1) vert.Color = ParseRGBA(row[indexDiffuse]);
                    if (indexNX != -1)
                    {
                        vert.NX = Parse<float>(row[indexNX]);
                        vert.NY = Parse<float>(row[indexNY]);
                        vert.NZ = Parse<float>(row[indexNZ]);
                    }
                    vertices.Add(vert);
                }
            }
            vertices.Sort((x, y) => x.Index.CompareTo(y.Index) );
            Console.WriteLine();
            using (var writer = new StreamWriter(extra[1]))
            {
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine($"element vertex {vertices.Count}");
                writer.WriteLine("property float x");
                writer.WriteLine("property float y");
                writer.WriteLine("property float z");
                if (indexNX != -1)
                {
                    writer.WriteLine("property float nx");
                    writer.WriteLine("property float ny");
                    writer.WriteLine("property float nz");
                }
                if (indexDiffuse != -1) {
                    writer.WriteLine("property uchar red");
                    writer.WriteLine("property uchar green");
                    writer.WriteLine("property uchar blue");
                    writer.WriteLine("property uchar alpha");
                }
                if (indexU != -1) {
                    writer.WriteLine("property float s");
                    writer.WriteLine("property float t");
                }
                writer.WriteLine($"element face {(indices.Count / 3)}");
                writer.WriteLine("property list uint8 int vertex_index");
                writer.WriteLine("end_header");
                foreach (var vtx in vertices)
                {
                    if(yUp)
                        writer.Write("{0} {1} {2}", vtx.X, vtx.Y, vtx.Z);
                    else
                        writer.Write("{0} {1} {2}", vtx.X, vtx.Z, -vtx.Y);
                    if (indexNX != -1)
                    {
                        if(yUp)
                            writer.Write("{0} {1} {2}", vtx.NX, vtx.NY, vtx.NZ);
                        else
                            writer.Write("{0} {1} {2}", vtx.NX, vtx.NZ, -vtx.NY);
                    }
                    if (indexDiffuse != -1)
                        writer.Write(" {0} {1} {2} {3}", vtx.Color.R, vtx.Color.G, vtx.Color.B, vtx.Color.A);
                    if (indexU != -1)
                        writer.Write(" {0} {1}", vtx.U, flipUvs ? 1 - vtx.V : vtx.V);
                    writer.WriteLine();
                }
                for (int i = 0; i < indices.Count; i += 3)
                {
                    writer.WriteLine($"3 {indices[i]} {indices[i + 1]} {indices[i + 2]}");
                }
            }
        }
    }
}
