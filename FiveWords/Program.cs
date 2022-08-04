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
            
        }

        private Dictionary<uint, List<string>> m_Words = new Dictionary<uint, List<string>> ();



        private void Run() {
            StreamReader reader = File.OpenText("C:/Dev/StandUpMath/FiveWords/words_alpha.txt");


            // convert all the 5 letter words in to bit masks.
            // (bit[0]=a, bit[1]=b bit[2]=c etc)
            // we ignore masks that don't have 5 bits set because that means there is a duplicated letter inside the word

            int wordCount = 0;
            while (!reader.EndOfStream) {
                string word = reader.ReadLine();
                if (word.Length == 5) {
                    (uint mask, bool dupe) = MakeMask(word);
                    if (!dupe) {
                        if (!m_Words.TryGetValue(mask, out List<string> anagrams)) {
                            anagrams = new List<string>();
                            m_Words[mask] = anagrams;
                        }

                        Console.WriteLine("{0} {1:X8}", word, mask);
                        anagrams.Add(word);
                        wordCount += 1;
                    }

                }
            }

            reader.Close();

            Console.Out.WriteLine("{0} words, {1} anagrams", wordCount, m_Words.Count);
            
            DateTime startTime = DateTime.Now;


            List<uint> maskList = m_Words.Keys.ToList();

            // sorting helps because it means that a clashing mask is likely to be close by in the list of words, which
            // means we are more likely to prune a whole branch early (This shaved off ~25% duration when I added it)

            // There is probably a whole PhD for someone in coming up with interesting comparison functions that 
            // result inf a "sorted" list that optimizes the search time. 
            maskList.Sort();

            uint[] masks = maskList.ToArray();

            DateTime sortTime = DateTime.Now;
            Console.Out.WriteLine("SortTime = {0}", sortTime - startTime);



            int size = masks.Length;
            int numProcessors = Environment.ProcessorCount * 4 ; // Why does * 4 give better results? where are we loosing time?
            double segmentArea = 0.5 / numProcessors;
            double prev = 0;
            
            // Try and split up the threads so that they each do roughly the same amount of work 
            // assume the work is evenly distributed across the problem space, and because we're doing
            // an N^2/2 triangle of work the thread that is doing the fdat end of the triangle will have to work harder
            // so try and even out the _area_ of the triangle that each thread is doing.
            
            // I'm not even sure this is right. It is late and I'm second guessing myself

            for (int i = 0; i < numProcessors - 1; i++) {

                double a = -0.5;
                double b = 1;
                double c = (prev * prev - 2.0 * prev) / 2.0 - segmentArea;

                double det = b * b - 4.0 * a * c;
                double x = (-b + Math.Sqrt(det)) / (2 * a);

       
                StartThread(masks, Convert.ToInt32(prev * size), Convert.ToInt32(x * size));


                prev = x;
            }

            // one last thread with a hard coded upper limit to guarantee we get every element in the array 
            StartThread(masks, Convert.ToInt32(prev * size), size); 

            WaitForThreads();

            DateTime endTime = DateTime.Now;

            int resultCount = DumpResults();



            Console.Out.WriteLine("Done! Found {0} ({1})", resultCount, endTime - startTime);
        }

        private int DumpResults() {
            int count = 0;
            foreach (ThreadedWorker worker in m_Workers)
            {
                foreach (List<uint> result in worker.Results) {
                    ReportResult(result);
                    count += 1;
                }
            }

            return count;
        }

        private void WaitForThreads() {
            foreach (Thread thread in m_Threads) {
                thread.Join();
            }
        }


        private List<ThreadedWorker> m_Workers = new List<ThreadedWorker>();
        private List<Thread> m_Threads = new List<Thread>();

        private void StartThread(uint[] masks, int firstIndex, int lastIndex) {
            ThreadedWorker worker = new ThreadedWorker(masks,firstIndex,lastIndex);
            Thread thread = new Thread(worker.DoWork);

            m_Workers.Add(worker);
            m_Threads.Add(thread);
            
            thread.Start();
        } 
        

        public class ThreadedWorker {
            private uint[] m_Masks;
            private int m_StartIndex;
            private int m_EndIndex;
            
            private List<List<uint>> m_Results = new List<List<uint>>();

            private uint[][] m_Buffers;


            public ThreadedWorker(uint[] masks,int startIndex,int endIndex) {
                m_Masks = masks;
                m_StartIndex = startIndex;
                m_EndIndex = endIndex;

            }

            public List<List<uint>> Results {
                get => m_Results;
                set => m_Results = value;
            }

            public void DoWork() {
                uint[] result = new uint[5];
            
                m_Buffers = new uint[5][];
                m_Buffers[0] = m_Masks;
                for (int i = 1; i < 5; i++) {
                    m_Buffers[i] = new uint[m_Masks.Length];
                }
            
            

                Recurse(0, m_StartIndex,m_EndIndex,m_Masks.Length, 0x0, result);


            }
            
            private void Recurse(in int depth,in int startIndex,in int endIndex,in int bufferCount, in uint resultMask, in uint[] result) { 
                // check all the remaining word-masks
                uint[] buffer = m_Buffers[depth];
                for(int i=startIndex;i<endIndex;i++) {
                    uint mask = buffer[i];
                    uint tempResultMask = resultMask | mask;
                    uint emptySpace = ~tempResultMask;
                    result[depth] = mask;
                    if (depth == 4) {
                        m_Results.Add(new List<uint>(result));
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
         
                        Recurse(nextBufferDepth,0,nextBufferCount,nextBufferCount,tempResultMask,result);                        
                    }
         
                    // if (depth == 0) {
                    //     Console.WriteLine("{0}/{1}",i,5977);
                    // }
                }
                result[depth] = 0;
            }            
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