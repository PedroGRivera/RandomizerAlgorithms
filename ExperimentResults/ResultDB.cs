﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RandomizerAlgorithms
{
    //Class acts as the interface to the database where experimental results are stored
    class ResultDB : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=C:\\Users\\Caleb\\Source\\Repos\\RandomizerAlgorithms\\ExperimentResults\\ResultsDB.db");

        public virtual DbSet<Result> Results { get; set; }
    }
}
