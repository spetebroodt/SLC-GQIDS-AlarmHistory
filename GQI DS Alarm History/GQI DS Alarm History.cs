/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/04/2025	1.0.0.1		Sebastiaan, Skyline	Initial version
****************************************************************************
*/

namespace GQIDSAlarmHistory
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using SLDataGateway.API.Querying;
	using SLDataGateway.API.Repositories.CustomDataTableConfiguration;
	using SLDataGateway.API.Repositories.Extensions;
	using SLDataGateway.API.Repositories.Registry;
	using SLDataGateway.API.Requests.Queries.Abstract;
	using SLDataGateway.API.Types.Paging;
	using SLDataGateway.API.Types.Repositories;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "History alarms")]
	public sealed class GQIDSHistoryAlarms : IGQIDataSource, IGQIOnInit, IGQIInputArguments, IGQIOnPrepareFetch, IGQIOnDestroy
	{
		private static readonly GQIDateTimeArgument _fromArg = new GQIDateTimeArgument("From") { IsRequired = true };
		private static readonly GQIDateTimeArgument _untilArg = new GQIDateTimeArgument("Until") { IsRequired = false };
		private static readonly GQIStringArgument _searchTermArg = new GQIStringArgument("Search term") { IsRequired = false };

		private GQIDMS _dms;
		private IConnection _connection;
		private IGQILogger _logger;
		private DateTime _from;
		private DateTime _until;
		private string _searchTerm;
		private IDatabaseRepositoryRegistry _registry;
		private IAlarmRepository _repository;
		private IDatabaseQuery<Alarm> _query;
		private DisposablePagingCookie _previousCookie = null;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_logger = args.Logger;
			_dms = args.DMS;
			return default;
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				_fromArg,
				_untilArg,
				_searchTermArg,
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_from = args.GetArgumentValue(_fromArg);
			args.TryGetArgumentValue(_untilArg, out _until);
			args.TryGetArgumentValue(_searchTermArg, out _searchTerm);

			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Severity"),
				new GQIStringColumn("Element"),
				new GQIStringColumn("Parameter"),
				new GQIStringColumn("Value"),
				new GQIDateTimeColumn("Time"),
				new GQIStringColumn("Root ID"),
				new GQIStringColumn("ID"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_connection = _dms.GetConnection();
			_registry = CreateRegistry(_connection);
			_repository = CreateRepository(_registry);

			var filter = CreateFilter();

			var query = filter.Limit(1_000_000)
				.OrderByDescending(AlarmExposers.TimeOfArrival);

			_query = _repository.CreateReadQuery(query)
				.SetTimeout(120_000)
				.SetPageSize(10_000)
				.SetAutoPage(false);

			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			_logger.Debug($"Read next page.");

			if (_previousCookie != null)
			{
				_query.SetPagingCookie(new DisposablePagingCookie(_previousCookie.GUID));
				_previousCookie.Dispose();
			}

			var alarmPage = _query.ExecutePaged();
			_previousCookie = alarmPage.PagingCookie as DisposablePagingCookie;

			var rows = alarmPage.Select(CreateRow).ToArray();
			return new GQIPage(rows)
			{
				HasNextPage = rows.Length > 0,
			};
		}

		public OnDestroyOutputArgs OnDestroy(OnDestroyInputArgs args)
		{
			_connection?.Dispose();
			_registry?.Dispose();
			_repository?.Dispose();
			return default;
		}

		private IDatabaseRepositoryRegistry CreateRegistry(IConnection connection)
		{
			IDatabaseRepositoryRegistry registry = null;
			try
			{
				registry = DatabaseRepositoryRegistry
						.Builder?
						.WithConnection(connection)?
						.WithCustomDataTableConfiguration(CustomDataTableConfiguration.Global)?
						.Build();
			}
			catch (Exception ex)
			{
				_logger.Error($"Error creating repository registry: {ex.Message}");
				throw new GenIfException($"Could not create repository registry. Exception '{ex.Message} - {ex.StackTrace} - {ex.InnerException}'.");
			}

			if (registry == null)
				throw new GenIfException("Could not create repository registry.");

			return registry;
		}

		private IAlarmRepository CreateRepository(IDatabaseRepositoryRegistry registry)
		{
			var repository = registry.Get<IAlarmRepository>();

			if (repository == null)
				throw new GenIfException("Could not create alarm repository.");

			return repository;
		}

		private FilterElement<Alarm> CreateFilter()
		{
			FilterElement<Alarm> filter = AlarmExposers.TimeOfArrival.GreaterThanOrEqual(_from);
			if (_until == default(DateTime))
			{
				_logger.Information($"Fetching alarm history from {_from.ToLongTimeString()} onwards.");
			}
			else
			{
				filter = filter.AND(AlarmExposers.TimeOfArrival.LessThan(_until));
				_logger.Information($"Fetching alarm history from {_from.ToString("F")} until {_until.ToString("F")}.");
			}

			if (!string.IsNullOrWhiteSpace(_searchTerm))
			{
				filter = filter.AND(AlarmExposers.ElementName.Contains(_searchTerm)
					.OR(AlarmExposers.ParameterName.Contains(_searchTerm))
					.OR(AlarmExposers.Value.Contains(_searchTerm)));
			}

			return filter;
		}

		private GQIRow CreateRow(Alarm alarm)
		{
			return new GQIRow(new GQICell[]
			{
				new GQICell() { Value = alarm.Severity }, // "Severity"
				new GQICell() { Value = alarm.ElementName }, // "Element"
				new GQICell() { Value = alarm.ParameterName }, // "Parameter"
				new GQICell() { Value = alarm.Value }, // "Value"
				new GQICell() { Value = alarm.TimeOfArrival.ToUniversalTime() }, // "Time"
				new GQICell() { Value = alarm.TreeID.ToString() }, // "Root ID"
				new GQICell() { Value = alarm.AlarmID.ToString() }, // "ID"
			});
		}
	}
}
