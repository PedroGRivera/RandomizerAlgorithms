﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace RandomizerAlgorithms
{
    //This class implements the searching methods used to compute reachability and trace a path through the game world
    class Search
    {
        Helpers helper;
        Parser parser;

        public Search()
        {
            helper = new Helpers();
            parser = new Parser();
        }

        //Constructor with pre-specified helper so it may be seeded
        //Currently not necessary since search uses no RNG
        public Search(Helpers h)
        {
            helper = h;
            parser = new Parser();
        }

        //Utilizes BFS search algorithm to find all locations in the world which are reachable with the current item set
        //Important note is that throughout this function the owned items are static, they are not collected throughout (as they are in sphere search)
        //It is used for forward search, where there is no need to check for items currently within R, as well as other places such as sphere search
        public WorldGraph GetReachableLocations(WorldGraph world, List<Item> owneditems)
        {
            WorldGraph reachable = new WorldGraph(world);
            Region root = world.Regions.First(x => x.Name == world.StartRegionName);
            Queue<Region> Q = new Queue<Region>();
            HashSet<Region> visited = new HashSet<Region>();
            Q.Enqueue(root);
            visited.Add(root);

            //Implementation of BFS
            while (Q.Count > 0)
            {
                Region r = Q.Dequeue();
                Region toadd = new Region(r);

                foreach (Exit e in r.Exits)
                {
                    //Normally in BFS, all exits would be added
                    //But in this case, we only want to add exits which are reachable
                    if (parser.RequirementsMet(e.Requirements, owneditems))
                    {
                        toadd.Exits.Add(e);
                        Region exitto = world.Regions.First(x => x.Name == e.ToRegionName); //Get the region this edge leads to
                        if (!visited.Contains(exitto)) //Don't revisit already visited nodes on this path
                        {
                            Q.Enqueue(exitto);
                            visited.Add(exitto);
                        }
                    }
                }
                //Subsearch to check each edge to a location in the current region
                //If requirement is met, add it to reachable locations
                foreach (Location l in r.Locations)
                {
                    if (parser.RequirementsMet(l.Requirements, owneditems))
                    {
                        toadd.Locations.Add(l);
                    }
                }
                reachable.Regions.Add(toadd); //Add every reachable exit and location discovered in this iteration
            }
            return reachable;
        }

        //Utilizes BFS search algorithm to find all locations in the world which are reachable with the current item set
        //In this algorithm, we want to check for items which have been removed from I but are still contained within R, so an initial search is done to collect items before returning the final search
        public WorldGraph GetReachableLocationsAssumed(WorldGraph world, List<Item> owneditems)
        {
            WorldGraph copy = world.Copy(); //Used so items may be removed from world at will
            List<Item> newitems = ItemSearch(copy, owneditems); //Find items within R
            List<Item> combined = owneditems.ToList(); //Copy list
            while (newitems.Count > 0)
            {
                combined.AddRange(newitems); //Add items to currently used items
                newitems = ItemSearch(copy, combined); //Find items within R
            }
            return GetReachableLocations(world, combined); //Use that combined list to find final search result
        }

        //Initially, went to collect all items which are reachable with the current item set and not already contained within the item set
        public List<Item> ItemSearch(WorldGraph world, List<Item> owneditems)
        {
            List<Item> newitems = new List<Item>();

            Region root = world.Regions.First(x => x.Name == world.StartRegionName);
            Queue<Region> Q = new Queue<Region>();
            HashSet<Region> visited = new HashSet<Region>();
            Q.Enqueue(root);
            visited.Add(root);

            //Implementation of BFS
            while (Q.Count > 0)
            {
                Region r = Q.Dequeue();

                foreach (Exit e in r.Exits)
                {
                    //Normally in BFS, all exits would be added
                    //But in this case, we only want to add exits which are reachable
                    if (parser.RequirementsMet(e.Requirements, owneditems))
                    {
                        Region exitto = world.Regions.First(x => x.Name == e.ToRegionName); //Get the region this edge leads to
                        if (!visited.Contains(exitto)) //Don't revisit already visited nodes on this path
                        {
                            Q.Enqueue(exitto);
                            visited.Add(exitto);
                        }
                    }
                }
                //Subsearch to check each edge to a location in the current region
                //If requirement is met, add it to reachable locations
                foreach (Location l in r.Locations)
                {
                    if (parser.RequirementsMet(l.Requirements, owneditems))
                    {
                        if(l.Item.Importance == 2) //If location contains a major item
                        {
                            newitems.Add(l.Item);
                            l.Item = new Item(); //Remove item so it isn't added again in future iterations
                        }
                    }
                }
            }
            return newitems;
        }

        List<List<Region>> paths = new List<List<Region>>();

        //Use DFS to find all possible paths from the root to the specified region
        //Not including paths that go back on themselves
        public List<List<Region>> PathsToRegion(WorldGraph world, Region dest)
        {
            Region root = world.Regions.First(x => x.Name == world.StartRegionName);

            List<Region> visited = new List<Region>();
            paths = new List<List<Region>>();
            if(root == dest) //If root and dest equal, return empty list
            {
                return paths;
            }

            RecursiveDFS(world, root, dest, visited); //Recursively run DFS, when dest found add the path to paths var

            return paths; //Return list of paths
        }

        //Recursively check exits with copy of visited list
        //It's done this way so that after the destination or a dead end is met, the code flow "backs up"
        public void RecursiveDFS(WorldGraph world, Region r, Region dest, List<Region> visited)
        {
            visited.Add(r); //Add to visited list
            if (r == dest)
            {
                paths.Add(visited); //If this is the dest, then visited currently equals a possible path
                return;
            }
            foreach (Exit e in r.Exits)
            {
                Region exitto = world.Regions.First(x => x.Name == e.ToRegionName); //Get the region this edge leads to
                if (!visited.Contains(exitto)) //Don't revisit already visited nodes on this path
                {
                    List<Region> copy = new List<Region>(visited); //If don't do this List is passed by reference, algo doesn't work
                    RecursiveDFS(world, exitto, dest, copy);
                }
            }

        }

        /*
         * Sphere Search is done iteratively in “Spheres” and is used to attempt to trace a path
         * from the beginning to the end of the game. The first sphere s is simply all locations which are
         * reachable from the beginning of the game. As it searches these locations, it adds key items
         * found to a temporary set; we do not want those items to affect reachability until the next sphere
         * iteration so we do not yet add them to I. After all reachable locations have been found, sphere s
         * is added to the list of spheres S, and all items in the temporary set are added to I. It then
         * iterates again with a new sphere s.
         */
        public SphereSearchOutput SphereSearch(WorldGraph world)
        {
            SphereSearchOutput output = new SphereSearchOutput();
            output.Spheres = new List<WorldGraph>();
            List<Item> owneditems = new List<Item>();
            //Initial sphere s0 includes items reachable from the start of the game
            WorldGraph s0 = GetReachableLocations(world, owneditems);
            owneditems = s0.CollectMajorItems(); //College all major items reachable from the start of the game
            output.Spheres.Add(s0); //Add initial sphere to sphere list
            //sx indicates every sphere after the first. Any major items found in s0 means sx should be bigger.
            WorldGraph sx = GetReachableLocations(world, owneditems);
            int temp = owneditems.Where(x => x.Importance >= 2).Count(); //Temp is the count of previously owned major items
            owneditems = sx.CollectAllItems(); //Used to find new count of major items
            //If counts are equal then no new major items found, stop searching
            while (owneditems.Where(x => x.Importance >= 2).Count() > temp) //If new count is not greater than old count, that means all currently reachable locations have been found
            {
                output.Spheres.Add(sx); //If new locations found, add to sphere list
                //Take the same steps taken before the loop: Get new reachable locations, collect new major items, and check to see if new count is larger than old count
                sx = GetReachableLocations(world, owneditems);
                temp = owneditems.Where(x => x.Importance >= 2).Count(); //Only want to consider count of major items
                owneditems = sx.CollectAllItems();
            }
            //At this point, either a dead end has been found or the end of the game has
            //If the goal item is in the list of owned items, means the end has been found and thus the game is completable
            output.Completable = owneditems.Count(x => x.Name == world.GoalItemName) > 0;
            return output;
        }
    }

    //Class to record list of spheres and completability bool
    struct SphereSearchOutput
    {
        public List<WorldGraph> Spheres;
        public bool Completable;
    }
}
