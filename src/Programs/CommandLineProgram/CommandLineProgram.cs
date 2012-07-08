// Copyright (C) 2012 Zeno Gantner
// 
// This file is part of MyMediaLite.
// 
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
// 
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mono.Options;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Util;

public abstract class CommandLineProgram<T> where T:IRecommender
{
	protected T recommender;

	protected OptionSet options;

	// command line parameters
	protected string data_dir = string.Empty;
	protected string training_file;
	protected string test_file;
	protected string save_model_file;
	protected string load_model_file;
	protected string save_user_mapping_file;
	protected string save_item_mapping_file;
	protected string load_user_mapping_file;
	protected string load_item_mapping_file;
	protected string user_attributes_file;
	protected string item_attributes_file;
	protected string user_relations_file;
	protected string item_relations_file;
	protected string prediction_file;
	protected bool compute_fit = false;
	protected bool no_id_mapping = false;
	protected int random_seed = -1;
	protected uint cross_validation;
	protected double test_ratio = 0;
	
	// recommender arguments
	protected string method              = null;
	protected string recommender_options = string.Empty;

	// help/version
	protected bool show_help    = false;
	protected bool show_version = false;
	
	// arguments for iteration search
	protected int max_iter   = 100;
	protected string measure;
	protected double epsilon = 0;
	protected double cutoff  = double.MaxValue;
	protected int find_iter = 0;
	
	// ID mapping objects
	protected IEntityMapping user_mapping = new EntityMapping();
	protected IEntityMapping item_mapping = new EntityMapping();

	// user and item attributes
	protected SparseBooleanMatrix user_attributes;
	protected SparseBooleanMatrix item_attributes;
	
	// time statistics
	protected List<double> training_time_stats = new List<double>();
	protected List<double> fit_time_stats      = new List<double>();
	protected List<double> eval_time_stats     = new List<double>();

	protected virtual void Usage(string message)
	{
		Console.WriteLine(message);
		Console.WriteLine();
		Usage(-1);
	}

	protected abstract void Usage(int exit_code);

	protected abstract void SetupOptions();

	protected abstract void ShowVersion();
	
	protected virtual void CheckParameters(IList<string> extra_args)
	{
		if (cross_validation == 1)
			Abort("--cross-validation=K requires K to be at least 2.");
		
		if (cross_validation > 1 && test_ratio != 0)
			Abort("--cross-validation=K and --test-ratio=NUM are mutually exclusive.");

		if (cross_validation > 1 && prediction_file != null)
			Abort("--cross-validation=K and --prediction-file=FILE are mutually exclusive.");

		if (cross_validation > 1 && save_model_file != null)
			Abort("--cross-validation=K and --save-model=FILE are mutually exclusive.");

		if (cross_validation > 1 && load_model_file != null)
			Abort("--cross-validation=K and --load-model=FILE are mutually exclusive.");
		
		if (recommender is IUserAttributeAwareRecommender && user_attributes_file == null)
			Abort("Recommender expects --user-attributes=FILE.");

		if (recommender is IItemAttributeAwareRecommender && item_attributes_file == null)
			Abort("Recommender expects --item-attributes=FILE.");

		if (recommender is IUserRelationAwareRecommender && user_relations_file == null)
			Abort("Recommender expects --user-relations=FILE.");

		if (recommender is IItemRelationAwareRecommender && user_relations_file == null)
			Abort("Recommender expects --item-relations=FILE.");
		
		if (no_id_mapping)
		{
			if (save_user_mapping_file != null)
				Abort("--save-user-mapping=FILE and --no-id-mapping are mutually exclusive.");
			if (save_item_mapping_file != null)
				Abort("--save-item-mapping=FILE and --no-id-mapping are mutually exclusive.");
			if (load_user_mapping_file != null)
				Abort("--load-user-mapping=FILE and --no-id-mapping are mutually exclusive.");
			if (load_item_mapping_file != null)
				Abort("--load-item-mapping=FILE and --no-id-mapping are mutually exclusive.");
		}

		if (extra_args.Count > 0)
			Usage("Did not understand " + extra_args[0]);
	}
	
	protected virtual void Run(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Handlers.UnhandledExceptionHandler);
		Console.CancelKeyPress += new ConsoleCancelEventHandler(AbortHandler);
		
		options = new OptionSet() {
			// string-valued options
			{ "training-file=",       v              => training_file        = v },
			{ "test-file=",           v              => test_file            = v },
			{ "recommender=",         v              => method               = v },
			{ "recommender-options=", v              => recommender_options += " " + v },
			{ "data-dir=",            v              => data_dir             = v },
			{ "user-attributes=",     v              => user_attributes_file = v },
			{ "item-attributes=",     v              => item_attributes_file = v },
			{ "user-relations=",      v              => user_relations_file  = v },
			{ "item-relations=",      v              => item_relations_file  = v },
			{ "save-model=",          v              => save_model_file      = v },
			{ "load-model=",          v              => load_model_file      = v },
			{ "save-user-mapping=",   v              => save_user_mapping_file = v },
			{ "save-item-mapping=",   v              => save_item_mapping_file = v },
			{ "load-user-mapping=",   v              => load_user_mapping_file = v },
			{ "load-item-mapping=",   v              => load_item_mapping_file = v },
			{ "prediction-file=",     v              => prediction_file      = v },
			{ "measure=",             v              => measure              = v },
			// integer-valued options
			{ "find-iter=",           (int v)        => find_iter            = v },
			{ "max-iter=",            (int v)        => max_iter             = v },
			{ "random-seed=",         (int v)        => random_seed          = v },
			{ "cross-validation=",    (uint v)       => cross_validation     = v },
			// double-valued options
			{ "epsilon=",             (double v)     => epsilon              = v },
			{ "cutoff=",              (double v)     => cutoff               = v },
			{ "test-ratio=",          (double v)     => test_ratio           = v },
			// boolean options
			{ "compute-fit",          v => compute_fit       = v != null },
			{ "no-id-mapping",        v => no_id_mapping     = v != null },
			{ "help",                 v => show_help         = v != null },
			{ "version",              v => show_version      = v != null },
		};
		SetupOptions();
		
		IList<string> extra_args = options.Parse(args);
		if (show_version)
			ShowVersion();
		if (show_help)
			Usage(0);

		if (random_seed != -1)
			MyMediaLite.Util.Random.Seed = random_seed;
		
		CheckParameters(extra_args);
	}
	
	protected void Abort(string message)
	{
		Console.Error.WriteLine(message);
		Environment.Exit(-1);
	}

	protected void AbortHandler(object sender, ConsoleCancelEventArgs args)
	{
		DisplayStats();
	}

	protected void DisplayStats()
	{
		if (training_time_stats.Count > 0)
			Console.Error.WriteLine(
				string.Format(
					CultureInfo.InvariantCulture,
					"iteration_time: min={0:0.##}, max={1:0.##}, avg={2:0.##}",
					training_time_stats.Min(), training_time_stats.Max(), training_time_stats.Average()));
		if (eval_time_stats.Count > 0)
			Console.Error.WriteLine(
				string.Format(
					CultureInfo.InvariantCulture,
					"eval_time: min={0:0.##}, max={1:0.##}, avg={2:0.##}",
					eval_time_stats.Min(), eval_time_stats.Max(), eval_time_stats.Average()));
		if (compute_fit && fit_time_stats.Count > 0)
			Console.Error.WriteLine(
				string.Format(
					CultureInfo.InvariantCulture,
					"fit_time: min={0:0.##}, max={1:0.##}, avg={2:0.##}",
					fit_time_stats.Min(), fit_time_stats.Max(), fit_time_stats.Average()));
		Console.Error.WriteLine("memory {0}", Memory.Usage);
	}
}

