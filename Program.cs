﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace RandomizerAlgorithms
{
    class Program
    {
        const int trials = 10;
        //Random, forward, and assumed fill; set to true to test on that algo
        static bool[] dotests = { true, true, true };

        static void Main(string[] args)
        {
            Fill filler = new Fill();
            Search searcher = new Search();

            string jsontext = File.ReadAllText("../../../WorldGraphs/TestWorld.json");
            WorldGraph world = JsonConvert.DeserializeObject<WorldGraph>(jsontext);

            //string generatedjson = GenerateWorld(50, 30);

            Statistics teststatistics = new Statistics();
            ////double complexityscore = teststatistics.CalcWorldComplexity(world);

            //Search testsearcher = new Search();
            //testsearcher.PathsToRegion(world, world.Regions.First(x => x.Name == "Waterfall"));

            //Parser testparse = new Parser();
            //string result = testparse.Simplify("(Sword and Bow and Bow) or Has(Key,2)"); //Should be simplified to something like (Sword and Bow) or Has(Key,2)
            //string result2 = testparse.Simplify("Sword or Sword and Bow"); //Should be simplified to Sword
            ////majoritempool.RemoveAt(8);
            ////majoritempool.RemoveAt(0);
            //bool result = testparse.RequirementsMet("(Sword and Bow) or Has(Key,2)", majoritempool);

            //string testjsontext = File.ReadAllText("../../../WorldGraphs/TestWorldOriginal.json");
            //WorldGraph testworld = JsonConvert.DeserializeObject<WorldGraph>(testjsontext);
            //SphereSearchOutput testoutput = searcher.SphereSearch(testworld);
            //Print_Spheres(testoutput);

            //Loop for random fill
            if (dotests[0])
            {
                for (int i = 0; i < trials; i++)
                {
                    WorldGraph input = world.Copy(); //Copy so that world is not passed by reference and overwritten
                    List<Item> majoritempool = input.Items.Where(x => x.Importance == 2).ToList();
                    List<Item> minoritempool = input.Items.Where(x => x.Importance < 2).ToList();
                    WorldGraph randomgraph = filler.RandomFill(input, majoritempool); 
                    randomgraph = filler.RandomFill(randomgraph, minoritempool);

                    SphereSearchOutput output = searcher.SphereSearch(randomgraph);
                    //Print_Spheres(output);
                }
            }

            //Loop for forward fill
            if (dotests[1])
            {
                for (int i = 0; i < trials; i++)
                {
                    WorldGraph input = world.Copy(); //Copy so that world is not passed by reference and overwritten
                    List<Item> majoritempool = input.Items.Where(x => x.Importance == 2).ToList();
                    List<Item> minoritempool = input.Items.Where(x => x.Importance < 2).ToList();
                    WorldGraph forwardgraph = filler.ForwardFill(input, majoritempool); //Shuffle major items with logic
                    forwardgraph = filler.RandomFill(forwardgraph, minoritempool); //Shuffle minor items without logic

                    SphereSearchOutput output = searcher.SphereSearch(forwardgraph);
                }
            }

            //Loop for assumed fill
            if (dotests[2])
            {
                for (int i = 0; i < trials; i++)
                {
                    WorldGraph input = world.Copy(); //Copy so that world is not passed by reference and overwritten
                    List<Item> majoritempool = input.Items.Where(x => x.Importance == 2).ToList();
                    List<Item> minoritempool = input.Items.Where(x => x.Importance < 2).ToList();
                    WorldGraph assumedgraph = filler.AssumedFill(input, majoritempool); //Shuffle major items with logic
                    assumedgraph = filler.RandomFill(assumedgraph, minoritempool); //Shuffle minor items without logic

                    SphereSearchOutput output = searcher.SphereSearch(assumedgraph);
                }
            }
        }

        //This function outputs text for each sphere in the calculated sphere list
        //To avoid redundancy, major items will not be printed more than once
        //Sample output:
        /*
        Sphere 0:
        Forest_Chest: Bow
        Forest_Quest: Magic
        Field_Hidden A: Bombs
        Field_Hidden B: Sword
        City_Quest: GateKey

        Sphere 1:
        Field_Hidden C: GrapplingHook
        Lake_Quest: Sling

        Sphere 2:
        Valley_Chest: Key

        Sphere 3:
        Dungeon_Boss: Goal

        Is Completable: True
        */
        static void Print_Spheres(SphereSearchOutput input)
        {
            List<WorldGraph> spheres = input.Spheres;
            List<Item> MajorItemsFound = new List<Item>(); //Keeps track of already printed items to avoid redundancy
            int i = 0;
            foreach(WorldGraph sphere in spheres) //Go through each sphere
            {
                Console.WriteLine(Environment.NewLine + "Sphere " + i + ":"); //Print the sphere index
                i++;
                List<Item> majors = sphere.CollectAllItems().Where(x => x.Importance >= 2).ToList(); //Only check for major items
                foreach(Item m in majors)
                {
                    if(!MajorItemsFound.Contains(m)) //Check if each major item has been printed already; if not print
                    {
                        MajorItemsFound.Add(m);
                        //This loop is used to find the location of the item so that it may be printed as well
                        Location l = new Location();
                        foreach(Region r in sphere.Regions)
                        {
                            if(r.Locations.Count(x => x.Item == m) > 0)
                            {
                                l = r.Locations.First(x => x.Item == m);
                            }
                        }
                        Console.WriteLine(l.Name + ": " + m.Name); //Print the item location and name
                    }
                }
            }
            Console.WriteLine(Environment.NewLine + "Is Completable: " + input.Completable + Environment.NewLine); //After finishing, print if completable or not
        }

        //Generates a world with a specific count of regions and items
        //First generates many worlds to determine an average complexity then returns a world generated with a certain tolerance of that complexity
        //This takes a while to run, mainly because each complexity calculation takes ~2 seconds due to running the external python script
        static string GenerateWorld(int regioncount, int itemcount)
        {
            double goalcomplexity = AverageComplexity(regioncount, itemcount); //Get the average complexity of worlds generated with these parameters
            //Now generate worlds until one is generated within a certain tolerance of the average
            double tolerance = .10; //10%
            while (true)
            {
                //Generate a world, check its complexity
                WorldGenerator generator = new WorldGenerator(regioncount, itemcount);
                WorldGraph generated = generator.Generate();
                int test = generated.GetLocationCount();
                double complexity = generator.GetComplexity();
                if (goalcomplexity * (1-tolerance) < complexity && complexity < goalcomplexity * (1+tolerance))
                {
                    //Once complexity within x% of average has been generated, return json of the world so it can be saved
                    return generated.ToJson();
                }
            }
        }

        //Calculate the avrage complexity from generating many worlds with a specific regioncount and itemcount
        static double AverageComplexity(int regioncount, int itemcount)
        {
            //First do x trials to determine an average complexity
            int gentrials = 20;
            double totalcomplexity = 0;
            for (int i = 0; i < gentrials; i++)
            {
                WorldGenerator generator = new WorldGenerator(regioncount, itemcount);
                WorldGraph generated = generator.Generate();
                totalcomplexity += generator.GetComplexity();
            }
             return totalcomplexity / gentrials; //Determine average complexity, we want the goal complexity to be within some% of this
        }
    }
}
