//#define PACKETDUMP
//#define DETAILEDPACKETDUMP
//#define FIFORRDEBUG
//#define DEBUGBATCHER
//#define DETAILEDBATCHER
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace ICSimulator
{
    public class Channel
    {
        public int mem_id;
        public int id;
        public int numRanks;
        public int numBanks;

        public SchedBuf[] buf;
        public int coreRequests;
        public int GPURequests;
//        public int currentRequests { get {return coreRequests + GPURequests;} }
	/* HWA CODE */
        public int currentRequests { get {return coreRequests + GPURequests + HWARequests;} }

        public int readRequests;
        public int writeRequests;

        public int maxCoreRequests;
        public int maxGPURequests;
        public int maxRequests { get {return buf.Length;} }
        public int maxReads;
        public int maxWrites;
        public ulong [,] lastBankActivity;

        public DRAM mem;
        public Scheduler sched;

        public ulong[] IATCounter;
        public bool triggerCPUPrio = false;
        public ulong triggerDelay = 0;

        //stats related variable
        public int[] RBHCount;
        public int[] memServiceCount;
        public ulong[] insnsCount;
        public int statsInterval = 50000;
        public double[] BLP;
        public double[] BufferUsed;
        public int[] loadPerProc;

        public int ComboHitsCounts;

	/* HWA Code */
	public int HWARequests;
	public int maxHWARequests;
	public int HWAUnIssueRequests;
	public int[] unIssueRequestsPerCore;
	public int[] unIssueReadRequestsPerCore;
	public int[] RequestsPerBank;
	/* HWA Code End */

        public Channel(int mem_id, int id)
        {
            this.mem_id = mem_id;
            this.id = id;
            this.numRanks = Config.memory.numRanks;
            this.numBanks = Config.memory.numBanks;

            mem = new DRAM(this);

            this.IATCounter = new ulong[Config.Ng];
            this.RBHCount = new int[Config.Ng];
            this.insnsCount = new ulong[Config.Ng];
            this.memServiceCount = new int[Config.Ng];
            this.BLP = new double[Config.Ng];
            this.BufferUsed = new double[Config.Ng];
            this.loadPerProc = new int[Config.Ng];
            this.ComboHitsCounts = 0;

            for(int i=0;i<Config.Ng;i++)
                IATCounter[i]=0;
            // Scheduler
            buf = new SchedBuf[Config.memory.schedBufSize];
            for(int i=0;i<buf.Length;i++)
                buf[i] = new SchedBuf(i,mem);
 

            switch(Config.memory.DCTARBPolicy)
            {
                case "INVFCFS":
                    sched = new SchedInvFCFS(buf,mem,this); break;
                case "INVFRFCFS":
                    sched = new SchedInvFRFCFS(buf,mem,this); break;
                case "FCFS":
                    sched = new SchedFCFS(buf,mem,this); break;
                case "FRFCFS":
                    sched = new SchedFRFCFS(buf,mem,this); break;
                case "CoreID":
                    sched = new SchedCoreID(buf,mem,this); break;
                case "GFRFCFS":
                    sched = new SchedGFRFCFS(buf,mem,this); break;
                case "INVMPKI":
                    sched = new SchedInvMPKI(buf,mem,this); break;
                case "MPKI":
                    sched = new SchedMPKI(buf,mem,this); break;
                case "PARBS":
                    sched = new SchedPARBS(buf,mem,this); break;
                case "GPARBS":
                    sched = new SchedGPARBS(buf,mem,this); break;
                case "FRFCFS_PrioCPU":
                    sched = new SchedFRFCFS_PrioCPU(buf,mem,this); break;
                case "FRFCFS_CPUBURST":
                    sched = new SchedFRFCFS_PrioCPUWhenNonBursty(buf,mem,this); break;
                case "BLP":
                    sched = new SchedBLP(buf,mem,this); break;
                case "CTCM":
                    sched = new SchedCTCM(buf,mem,this); break;
                case "INVTCM":
                    sched = new SchedInvTCM(buf,mem,this); break;
                case "TCM":
                    sched = new SchedTCM(buf,mem,this); break;
                case "ATLAS":
                    sched = new ATLAS(buf,mem,this); break;
		/* HWA CODE */
                case "FRFCFS_DEADLINE":
                    sched = new SchedFRFCFSDeadLine(buf,mem,this); 
		    Console.WriteLine("FRFCFS_DeadLine selected");
		    break;
                case "FRFCFS_PRIORHWA":
                    sched = new SchedFRFCFSwithPriorHWA(buf,mem,this); 
		    Console.WriteLine("FRFCFS_withPriorHWA selected");
		    break;
                case "TCM_PRIORHWA":
                    sched = new SchedTCMwithPriorHWA(buf,mem,this);
		    Console.WriteLine("TCM_PriorHWA selected");
		    break;
                case "TCM_CLUSTEROPT":
                    sched = new SchedTCMClusterOpt(buf,mem,this);
		    Console.WriteLine("TCM_clusterOpt selected");
		    break;
                case "TCM_CLUSTEROPTPROB4":
                    sched = new SchedTCMClusterOptProb4(buf,mem,this);
		    Console.WriteLine("TCM_clusterOptProb4 selected");
		    break;
                case "TCM_DEADLINE":
                    sched = new SchedTCMDeadLine(buf,mem,this);
		    Console.WriteLine("TCM_deadline selected");
		    break;

		/* HWA CODE END */
                default:
                    Console.Error.WriteLine("Unknown DCT ARB Policy \"{0}\"",Config.memory.DCTARBPolicy);
                    Environment.Exit(-1);
                    break;
            }

            coreRequests = 0;
            GPURequests = 0;
            readRequests = 0;
            writeRequests = 0;
	    /* HWA CODE */
	    HWARequests = 0;
	    HWAUnIssueRequests = 0;
	    unIssueRequestsPerCore = new int[Config.Ng];
	    unIssueReadRequestsPerCore = new int[Config.Ng];
	    RequestsPerBank = new int[numBanks];

	    for(int i = 0; i < Config.Ng; i++ )
	    {
		unIssueRequestsPerCore[i] = 0;
		unIssueReadRequestsPerCore[i] = 0;
	    }
	    for(int i = 0; i < numBanks; i++ )
		RequestsPerBank[i] = 0;
	    /* HWA CODE End */

            lastBankActivity = new ulong[numRanks,numBanks];
            for(int r=0;r<numRanks;r++)
                for(int b=0;b<numBanks;b++)
                    lastBankActivity[r,b] = 0;

	    /* HWA Code Comment Out */
	    /*
            maxCoreRequests = buf.Length - Config.memory.reservedGPUEntries;
            if(maxCoreRequests < 8) maxCoreRequests = 8;
            maxGPURequests = buf.Length - Config.memory.reservedCoreEntries;
            if(maxGPURequests < 8) maxGPURequests = 8;
	    */
	    /* HWA Code Comment Out End */
            maxReads = Config.memory.RDBSize;
            maxWrites = Config.memory.WDBSize;
	    /* HWA Code */
            maxCoreRequests = buf.Length - Config.memory.reservedGPUEntries - Config.memory.reservedHWAEntries;
            if(maxCoreRequests < 8) maxCoreRequests = 8;
            maxGPURequests = buf.Length - Config.memory.reservedCoreEntries - Config.memory.reservedHWAEntries;
            if(maxGPURequests < 8) maxGPURequests = 8;
	    maxHWARequests = buf.Length - Config.memory.reservedCoreEntries - Config.memory.reservedGPUEntries;
	    if( maxHWARequests < 8 ) maxHWARequests = 8;

	    Console.WriteLine("maxRequests {0},{1},{2}",maxCoreRequests,maxGPURequests,maxHWARequests);
	    /* HWA Code End */
        }

        public void Enqueue(MemoryRequest mreq)
        {
            mreq.timeOfArrival = Simulator.CurrentRound;
//            Console.WriteLine("Incrementing load at src {0}", mreq.request.requesterID);
            loadPerProc[mreq.request.requesterID]++;
            // Walk array until empty entry is found; make sure to record buf index in mreq.
            for(int i=0;i<maxRequests;i++)
            {
#if PACKETDUMP
            Console.WriteLine("Enqueueing mreq from {0} to the buffers", mreq.request.requesterID);
#endif
	     if(buf[i].Available)
                {
                    buf[i].Allocate(mreq);
		    /* HWA Code */ // bug fixed??
//                    if(mreq.from_GPU)
		    if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() )
		    /* HWA Code End */
                        GPURequests++;
		    /* HWA Code */
                    else if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA() )
		    {
			HWARequests++;
			HWAUnIssueRequests++;
		    }
		    
		    /* HWA Code End */
		    else
                        coreRequests++;

		    RequestsPerBank[mreq.bank_index]++;
		    Simulator.QoSCtrl.RequestsPerBank[mreq.bank_index]++;
		    if( !Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA() )
			Simulator.QoSCtrl.CPURequestsPerBank[mreq.bank_index]++;
		

//		    Console.WriteLine("EnQueue: CPU:{0},GPU:{1},HWA:{2}", coreRequests, GPURequests, HWARequests);
		    unIssueRequestsPerCore[mreq.request.requesterID]++;
		    if( !mreq.isWrite )
			unIssueReadRequestsPerCore[mreq.request.requesterID]++;
		    Simulator.QoSCtrl.mem_req_enqueue(mreq.request.requesterID, mreq.request.address, mem_id);
//		    Console.WriteLine("EnQueue({0}) from {1}, addr:{2:x}", mem_id, mreq.request.requesterID, mreq.request.address);
                    return;
                }
            }
            Console.WriteLine("Failed to allocate ({0} req): {1} coreRequests, {2} GPUrequests, {3} buf size ({4} current reqs)",
                    mreq.from_GPU?"GPU":"core", coreRequests, GPURequests, buf.Length, currentRequests);
            System.Environment.Exit(-1);
        }

        public void Tick()
        {
//Sample BLP           
            for(int i=0;i<maxRequests;i++)
            {
                if(buf[i].Busy)
                {
                    BLP[buf[i].mreq.request.requesterID]++;
                    if(Simulator.network.nodes[buf[i].mreq.request.requesterID].m_cpu.m_stats_active)
                        Simulator.stats.BLPTotal[buf[i].mreq.request.requesterID].Add();
                    if(Simulator.network.nodes[buf[i].mreq.request.requesterID].m_cpu.m_stats_active)
                        Simulator.stats.DRAMUtilization[buf[i].mreq.request.requesterID].Add();
                }
            }
            for(int i=0;i<Config.Ng;i++)
            {
                IATCounter[i]++;
                BufferUsed[i] += (float)loadPerProc[i]/(float)maxRequests;
//                Console.WriteLine("LoadperProc[{0}] = {1}, BLP[{0}] = {2}",i, loadPerProc[i], BLP[i]);
            }
            if(IATCounter[Config.Ng-1]>Config.IAT_threshold)
                triggerCPUPrio = true;
            else
            {
                if(triggerDelay < Config.IAT_thres_delay)
                    triggerDelay++;  
                else
                {
                    triggerDelay = 0;
                    triggerCPUPrio = false;
                }
            }
            // anyone done?  invoke cb()
            for(int i=0;i<maxRequests;i++)
            {
                if(buf[i].Valid && buf[i].Completed)
                {
#if FIFORRDEBUG
    Console.WriteLine("Mem req. from src {0} finish at {1} (this is where we deallocate buffers queue)", buf[i].mreq.request.requesterID, Simulator.CurrentRound);
#endif
                    MemoryRequest mreq = buf[i].mreq;
                    // For RBH and BLP stats
                    memServiceCount[mreq.request.requesterID]++;
//                    Console.WriteLine("Decrementing load at src {0}", mreq.request.requesterID);
                    loadPerProc[mreq.request.requesterID]--;
		    /* HWA Code */ // bug fixed??
//                    if(mreq.from_GPU)
		    if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() )
		    /* HWA Code End */
                        GPURequests--;
		    /* HWA Code */
                    else if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA() )
			HWARequests--;
		    /* HWA Code End */
                    else
                        coreRequests--;
                    if(mreq.request.write) {
                        writeRequests--;
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                        Simulator.stats.DRAMWritesPerSrc[mreq.request.requesterID].Add();
                        if(!buf[i].issuedActivation)
                        {
                            RBHCount[mreq.request.requesterID]++;
                        }
                    } else {
                        readRequests--;
                        if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                            Simulator.stats.DRAMReadsPerSrc[mreq.request.requesterID].Add();
                        if(!buf[i].issuedActivation)
                        {
                            RBHCount[mreq.request.requesterID]++;
                        }
                    }

                    // Stats in Common/stats.cs
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                    Simulator.stats.DRAMTotalLatencyPerSrc[mreq.request.requesterID].Add(Simulator.CurrentRound-buf[i].whenArrived);
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                    Simulator.stats.DRAMTotalArrayLatencyPerSrc[mreq.request.requesterID].Add(Simulator.CurrentRound-buf[i].whenStarted);
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                    Simulator.stats.DRAMTotalQueueLatencyPerSrc[mreq.request.requesterID].Add(buf[i].whenStarted-buf[i].whenArrived);

		    // 
		    RequestsPerBank[mreq.bank_index]--;
		    Simulator.QoSCtrl.RequestsPerBank[mreq.bank_index]--;
		    if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA() )
			Simulator.QoSCtrl.CPURequestsPerBank[mreq.bank_index]--;

                    if(!buf[i].issuedActivation)
                    {
                        this.ComboHitsCounts++;
                    }
                    else
                    {
                        if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                            Simulator.stats.ComboHitsBin.Add(ComboHitsCounts);
                        this.ComboHitsCounts = 0;
                    }
                                        //InterArrvalTime stat
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                    Simulator.stats.CumulativeArrivalTime[mreq.request.requesterID].Add(IATCounter[mreq.request.requesterID]);
                    //Simulator.stats.ArrivaltimeBin[mreq.request.requesterID].Add((float)IATCounter[mreq.request.requesterID]);
                    if(Simulator.network.nodes[mreq.request.requesterID].m_cpu.m_stats_active)
                    Simulator.stats.ArrivaltimeBin.Add((float)IATCounter[mreq.request.requesterID]);
                    IATCounter[mreq.request.requesterID]=0;

                    //Deallocation and callback
		    /* HWA CODE */
//		    if( mreq.request.requesterID == 17 )
//			buf[i].print_stat(mem_id);
		    /* HWA CODE END */
                    buf[i].Deallocate();
                    mreq.cb();
                    break;
                }
            }
	    for( int i = 0; i < numBanks; i++ )
	    {
		Simulator.QoSCtrl.dram_bank_req_cnt[Config.memory.numChannels*mem_id+id,i] += (ulong)RequestsPerBank[i];
	    }
	    Simulator.QoSCtrl.dram_bank_req_cnt_base[Config.memory.numChannels*mem_id+id]++;

            sched.Tick();
        }

        public bool RequestEnqueueable(MemoryRequest mreq)
        {
            // Check either maxCoreRequests or maxGPURequests, and also check the sum
            // of current coreRequests and GPURequests.
#if DETAILEDPACKETDUMP
            Console.WriteLine("in RequestEnqueueable, mreq from {0}, GPURequests = {1}, maxGPURequests = {2}, currentRequests = {3}, maxRequests = {4}, coreRequests = {5}, maxCoreRequests = {6}",mreq.request.requesterID, GPURequests, maxGPURequests, currentRequests, maxRequests, coreRequests, maxCoreRequests);
#endif
	    /* HWA Code */ // bug fixed??
	    //                    if(mreq.from_GPU)
	    if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() )
		/* HWA Code End */
                return (GPURequests < maxGPURequests) && (currentRequests < maxRequests);
	    /* HWA Code */
	    else if( Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA() )
                return (HWARequests < maxHWARequests) && (currentRequests < maxRequests);
	    /* HWA Code End */
            else
                return (coreRequests < maxCoreRequests) && (currentRequests < maxRequests);
        }

    }   // Channel


    /**
     * The memory
     */
    public class MemCtlr
    {
        public Node node; //containing node

        //mem index
        static int index = 0;       ///total number of memories
        public int mem_id;          ///index of this memory

        // the trigger to enable batching, turns on when there is a queueing delay
        public bool[] triggerBatching;

        //size
        public int numChannels;
        public int numRanks;
        public int numBanks;

        //components
	/* HWA CODE */
//        public Queue<MemoryRequest>[] queueFromCores; 
        public List<MemoryRequest>[] queueFromCores;  // in order to overtake 
	int [] master_idx;
	int cur_tgt;
	int cur_ch;
	/* HWA CODE END */

        public Channel[] channels;

        protected int RRCounter = 0;
        protected int reRandomizeCountdown;
        protected const int randomizationInterval = 100000;
        protected int RRDirection = 1;
        protected Random randomizer;

        protected int ratioCounter = 0;

        /**
         * Constructor
         */
        public MemCtlr(Node node)
        {

            this.node = node;

           //memory id
            mem_id = index++;

            //per-channel state
            numChannels = Config.memory.numChannels;
            numRanks = Config.memory.numRanks;
            numBanks = Config.memory.numBanks;
            this.triggerBatching = new bool[numChannels];
            for(int i=0;i<numChannels;i++)
                triggerBatching[i] = false;

 
	    /* HWA CODE */
//            queueFromCores = new Queue<MemoryRequest>[numChannels];
//            for(int i=0;i<numChannels;i++)
//                queueFromCores[i] = new Queue<MemoryRequest>();
            queueFromCores = new List<MemoryRequest>[numChannels];
            for(int i=0;i<numChannels;i++)
                queueFromCores[i] = new List<MemoryRequest>();
	    /* HWA CODE */

            channels = new Channel[numChannels];
            for(int i=0;i<numChannels;i++)
                channels[i] = new Channel(mem_id,i);

            reRandomizeCountdown = randomizationInterval;
            randomizer = new Random();
        }

        public void ReceivePacket(MemoryRequest mreq)
        {
	    /* HWA CODE */
//            queueFromCores[mreq.channel_index].Enqueue(mreq);
            queueFromCores[mreq.channel_index].Add(mreq);
	    /* HWA CODE END */
        }

        public void access(Request req, Simulator.Ready cb)
        {
            MemoryRequest mreq = new MemoryRequest(req, cb);
            ReceivePacket(mreq);
        }


        public bool goThroughBatcher()
        {
            if(Config.cpuByPassBatcher)
                return false;
            if(Simulator.rand.NextDouble()<Config.cpuBatchedRate) 
                return true;
            else return false;
        }

        // checkQueueingDelay will check how long is the request queues in each channel. 
        // returns true if it is lone and should enable batching; otw, returns false
        public bool checkQueueingDelay(Channel chan)
        {
            int sum = 0;
            for(int i=0;i<chan.buf.Length;i++)
            {
                if(chan.buf[i].Valid) sum++;
            }
            if(sum > Config.triggerBatchingThresh) return true;
            else return false;
        }

        /**
        * Progresses time for the memory system (scheduler, banks, bus)
        */
        public void doStep()
        {
#if DETAILEDDEBUGBATCHER
    Console.WriteLine("Ticking in MemCtlr at {0} ", Simulator.CurrentRound);
#endif
            int sum = 0;
            for(int j=0;j<channels.Length;j++)
                for(int i=0;i<channels[j].buf.Length;i++)
                    if(channels[j].buf[i].Valid) sum++;
            Simulator.stats.DRAMBufferUtilization.Add(sum);

            ratioCounter++;
            if(ratioCounter >= Config.memory.busRatio)
                ratioCounter = 0;
            else
                return;

            // check if there's room for the requests from the cores
	    bool enqueue_flag =false;

            for(int i=0;i<numChannels;i++)
            {
                int idx = (i + RRCounter) % numChannels;
                // check to see if there's enough room in the channel's buffer
                // if so, send it on and remove from input queue
                if(queueFromCores[idx].Count > 0)
                {
		    /* HWA CODE */
		    if( Config.is_memQueue_priority_scheme )
		    {
			int[] pid;
			master_idx = new int[Config.Ng];
			pid = new int[Config.Ng];
//			Console.WriteLine("Start");
			for( cur_tgt = 0; cur_tgt < Config.Ng; cur_tgt++ )
			{
			    master_idx[cur_tgt] = queueFromCores[idx].FindIndex(FindCoreIdx);
			    pid[cur_tgt] = cur_tgt;
//			    Console.WriteLine("pid:{0}, qid:{1}", cur_tgt, master_idx[cur_tgt]);
			}
			cur_ch = idx;
			calculate_priority();
			Array.Sort(pid, sort_priority);
//			for( int pri = Config.Ng-1; pri >= 0; pri-- )
//			    Console.WriteLine("Pri:{0}, pid:{1}, pri:{2}", pri, pid[pri],channels[cur_ch].sched.getPriority(pid[pri]) );
			for( int pri = Config.Ng-1; pri >= 0; pri-- )
			{
			    if( master_idx[pid[pri]] >= 0 )
			    {
				MemoryRequest mreq = queueFromCores[idx][master_idx[pid[pri]]];
				if(RequestEnqueueable(mreq))
				{
//				    Console.WriteLine("Enqueue-pid:{0}",pid[pri]);
				    #if PACKETDUMP
				    Console.WriteLine("Enqueue to req.buffers req from {0}(cyc {1})",
						      mreq.request.requesterID, Simulator.CurrentRound);
				    #endif
 
				    queueFromCores[idx].RemoveAt(master_idx[pid[pri]]);
				    Enqueue(mreq);
				    enqueue_flag = true;
				    break;
				}
			    }
			}
			if( enqueue_flag ) break;
		    }
		    else if( Config.is_memQueue_InEntrance_Overtake )
		    {
			master_idx = new int[3];
			master_idx[0] = queueFromCores[idx].FindIndex(FindCpuReq); // CPU
			master_idx[1] = queueFromCores[idx].FindIndex(FindGpuReq); // GPU
			master_idx[2] = queueFromCores[idx].FindIndex(FindHwaReq); // HWA
			Array.Sort(master_idx);
			for( int m = 0; m < 3; m++ )
			{
			    if( master_idx[m] >= 0 )
			    {
				MemoryRequest mreq = queueFromCores[idx][master_idx[m]];
				if(RequestEnqueueable(mreq))
				{
				    #if PACKETDUMP
				    Console.WriteLine("Enqueue to req.buffers req from {0}(cyc {1})",
						      mreq.request.requesterID, Simulator.CurrentRound);
				    #endif
 
				    queueFromCores[idx].RemoveAt(master_idx[m]);
				    Enqueue(mreq);
				    enqueue_flag = true;
				    break;
				}
			    }
			}
			if( enqueue_flag ) break;
		    }
		    else
		    {
			// MemoryRequest mreq = queueFromCores[idx].Peek();
			MemoryRequest mreq = queueFromCores[idx][0];
			if(RequestEnqueueable(mreq))
			{
			    #if PACKETDUMP
			    Console.WriteLine("Enqueue to req.buffers req from {0}(cyc {1})",
					      mreq.request.requesterID, Simulator.CurrentRound);
			    #endif
 
//			    queueFromCores[idx].Dequeue();
			    queueFromCores[idx].RemoveAt(0);
			    Enqueue(mreq);
			    break;
			}
		    }
                }
            }

            // step each of the channels
            for(int i=0;i<numChannels;i++)
            {
                int idx = (i + RRCounter) % numChannels;
                channels[idx].Tick();
            }

            // round-robining/randomization for the channel loops
            reRandomizeCountdown--;
            if(reRandomizeCountdown <= 0)
            {
                RRCounter = randomizer.Next(numChannels);
                reRandomizeCountdown = randomizationInterval;
                RRDirection *= -1;
            }
            RRCounter += RRDirection;
            if(RRCounter < 0)
                RRCounter = numChannels-1;
            else if(RRCounter >= numChannels)
                RRCounter = 0;
        }
        
        // Accept a request and enqueue into corresponding channel.
        // This method assumes you've already checked that the
        // request is acceptable/enqueable.
        public void Enqueue(MemoryRequest mreq)
        {
            channels[mreq.channel_index].Enqueue(mreq);
        }

        public bool RequestEnqueueable(MemoryRequest mreq)
        {
            return channels[mreq.channel_index].RequestEnqueueable(mreq);
        }

	private bool FindCpuReq ( MemoryRequest mreq )
	{
	    return(!Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() &&
		   !Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA());
	}
	private bool FindGpuReq ( MemoryRequest mreq )
	{
	    return(Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() &&
		   !Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA());
	}
	private bool FindHwaReq ( MemoryRequest mreq )
	{
	    return(!Simulator.network.nodes[mreq.request.requesterID].cpu.is_GPU() &&
		   Simulator.network.nodes[mreq.request.requesterID].cpu.is_HWA());
	}
	private bool FindCoreIdx ( MemoryRequest mreq )
	{
	    return(mreq.request.requesterID==cur_tgt);
	}
	private void calculate_priority()
	{
	    channels[cur_ch].sched.calculate_priority();
	}

	public int sort_priority( int pid1, int pid2 )
	{
	    // return 1 is first argument is greater (higher)
	    int priority1 = channels[cur_ch].sched.getPriority(pid1);
	    int priority2 = channels[cur_ch].sched.getPriority(pid2);
	    if( priority1 != priority2 )
	    {
		if( priority1 > priority2 ) 
		{
		    return 1;
		    
		}
		else
		{ 
		    return -1;
		    
		}
	    }
	    else
	    {
		if( master_idx[pid1] ==  master_idx[pid2] ) return 0;
		if( master_idx[pid1] < master_idx[pid2] ) return 1;
		else return -1;
	    }
	    
	}


        /**
         * Report statistics
         */
        public void report(TextWriter writer)
        {
            // TODO: add stuff here
            writer.WriteLine("MemoryController[{0}]---------------------------------------------",mem_id);
            writer.WriteLine("MemoryController[{0}].requests:{1}",mem_id,-1);
        }

    }   //class MemController
}
