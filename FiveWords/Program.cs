using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FiveWords {
    class Program {
        static void Main(string[] args) {
            Program program = new Program();
            program.Run();

            // double[] v = new[] {
            //     0,
            //     0.075,
            //     0.15,
            //     0.3,
            //     0.45,
            //     0.6,
            //     1,
            // };
            //
            // double totalArea = (1 * 1) / 2.0;
            // double segmentArea = totalArea / 6.0;
            //
            // double prev = 0;
            //
            // for (int i = 0; i < 6; i++) {
            //
            //     double a = -1;
            //     double b = 2;
            //     double c = prev * prev - 2.0 * prev - segmentArea;
            //
            //     double det = b * b - 4.0 * a * c;
            //     double x = (-b + Math.Sqrt(det)) / 2 * a;
            //
            //     // double numProcessors = 8.27;
            //     //
            //     // double lowerE = Math.Pow(numProcessors,lower);
            //     // double upperE = Math.Pow(numProcessors,upper);
            //     //
            //     // double lowerNorm = (lowerE - 1.0) / (numProcessors - 1.0); 
            //     // double upperNorm = (upperE - 1.0) / (numProcessors - 1.0);
            //
            //     double lowerNorm = prev;
            //     double upperNorm = x;
            //
            //     double range = upperNorm - lowerNorm;
            //         
            //     double size = 5977;
            //
            //     double lowerI = lowerNorm;
            //     double upperI = upperNorm;
            //     double lowerTip = size - lowerI;
            //     double upperTip = size - upperI;
            //     double area = (upperI - lowerI) * (upperTip + lowerTip) / 2.0;
            //     
            //     
            //     //Console.WriteLine("i={0} E=({1}, {2}) norm=({3}, {4}) range={5} I=({6}, {7}) trip=({8}, {9}) area={10:#,##0.00}",i,lowerE,upperE,lowerNorm,upperNorm,range,lowerI,upperI,lowerTip,upperTip,area);
            //     Console.WriteLine("i={0} norm=({1}, {2}) range={3} I=({4}, {5}) trip=({6}, {7}) area={8:#,##0.00}",i,lowerNorm,upperNorm,range,lowerI,upperI,lowerTip,upperTip,area);
            //
            //     prev = x;
            // }

            
        }

        private Dictionary<uint, List<string>> m_Words = new Dictionary<uint, List<string>> ();
        private List<List<uint>> m_Results = new List<List<uint>>();

        private uint[][] m_Buffers;


        private void Run() {
            StreamReader reader = File.OpenText("C:/Dev/StandUpMath/FiveWords/words_alpha.txt");

            
            // convert all the 5 letter words in to bit masks.
            // (bit[0]=a, bit[1]=b bit[2]=c etc)
            // we ignore masks that don't have 5 bits set because that means there is a duplicated letter inside the word
            
            int wordCount = 0;
            while (!reader.EndOfStream) {
                string word = reader.ReadLine();
                if (word.Length == 5) {
                    (uint mask,bool dupe) = MakeMask(word);
                    if (!dupe) {
                        if (!m_Words.TryGetValue(mask, out List<string> anagrams)) {
                            anagrams = new List<string>();
                            m_Words[mask] = anagrams;
                        }
                        Console.WriteLine("{0} {1:X8}",word,mask);
                        anagrams.Add(word);
                        wordCount += 1;
                    }

                }
            }
            reader.Close();
            
            Console.Out.WriteLine("{0} words, {1} anagrams",wordCount,m_Words.Count);

            List<uint> masks = m_Words.Keys.ToList();

            uint[] results = new uint[5];

  


            DateTime startTime = DateTime.Now;
            
            m_Buffers = new uint[5][];
            for (int i = 0; i < 5; i++) {
                m_Buffers[i] = new uint[m_Words.Count];
            }

            
            // sorting helps because it means that a clashing mask is likely to be close by in the list of words, which
            // means we are more likely to prune a whole branch early (This shaved off ~25% duration when I added it)
            
            // There is probably a whole PhD for someone in coming up with interesting comparison functions that 
            // result inf a "sorted" list that optimizes the search time. 
            masks.Sort(); 
            
            DateTime sortTime = DateTime.Now;
            Console.Out.WriteLine("SortTime = {0}",sortTime-startTime);

            uint[] baseBuffer = m_Buffers[0];
            for (int i = 0; i < masks.Count; i++) {
                baseBuffer[i] = masks[i];
            }


            //if (GC.TryStartNoGCRegion(200000000)) {
                Recurse(0, masks.Count, 0x0, results);
            //}
            //GC.EndNoGCRegion();


            DateTime endTime = DateTime.Now;

            foreach (List<uint> result in m_Results) {
                ReportResult(result);
            }

            Console.Out.WriteLine("Done! Found {0} ({1})",m_Results.Count,endTime-startTime);
        }
        
        

        private void Recurse(in int depth,in int bufferCount, in uint resultMask, in uint[] results) { 
            // check all the remaining word-masks
            uint[] buffer = m_Buffers[depth];
            for(int i=0;i<bufferCount;i++) {
                uint mask = buffer[i];
                uint tempResultMask = resultMask | mask;
                uint emptySpace = ~tempResultMask;
                results[depth] = mask;
                if (depth == 4) {
                    m_Results.Add(new List<uint>(results));
                }
                else {
                    // i+1 because we've already checked all possible combinations less than the current word
                    int nextBufferCount = 0;
                    int nextBufferDepth = depth + 1;
                    uint[] nextBuffer = m_Buffers[nextBufferDepth];
                    for (int j = i+1; j < bufferCount; j++) {
                        uint newMask = buffer[j];
                        if ((emptySpace & newMask) == newMask) {
                            // we're in the clear, no bit overlap between our accumulated mask and the next word
                            nextBuffer[nextBufferCount++] = newMask;
                        }
                    }

                    Recurse(nextBufferDepth,nextBufferCount,tempResultMask,results);                        
                }

                // if (depth == 0) {
                //     Console.WriteLine("{0}/{1}",i,5977);
                // }
            }
            results[depth] = 0;
        }

        private void ReportResult(in List<uint> result) {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < result.Count; i++) {
                if (i != 0) {
                    sb.Append(",");
                }
                if (m_Words.TryGetValue(result[i], out List<string> anagrams)) {
                    sb.Append(anagrams[0]);
                } else {
                    sb.AppendFormat("???{0:X8}???", result[i]);
                }
            }
            sb.Append("]");
            Console.WriteLine(sb.ToString());

        }

        private (uint,bool) MakeMask(string word) {
            uint mask = 0x0;
            bool dupe = false;
            foreach (char c in word.ToLowerInvariant()) {
                int index = c - 'a';
                uint newMask = mask | (1u << index);
                if (newMask == mask) {
                    dupe = true;
                }

                mask = newMask;

            }
            return (mask,dupe);
        }
    }
}