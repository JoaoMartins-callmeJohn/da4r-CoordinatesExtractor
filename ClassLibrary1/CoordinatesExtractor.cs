using Autodesk.Forge;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace CoordinatesExtractor
{
	[Regeneration(RegenerationOption.Manual)]
	[Transaction(TransactionMode.Manual)]
	public class CoordinatesExtractor : IExternalDBApplication
	{
		public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
		{
			return ExternalDBApplicationResult.Succeeded;
		}

		public ExternalDBApplicationResult OnStartup(ControlledApplication application)
		{
			DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
			return ExternalDBApplicationResult.Succeeded;
		}

		private void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
		{
			e.Succeeded = true;
			ExtractModelCoordinates(e.DesignAutomationData);
		}

		private void ExtractModelCoordinates(DesignAutomationData designAutomationData)
		{
			if (designAutomationData == null) throw new ArgumentNullException(nameof(designAutomationData));

			Application rvtApp = designAutomationData.RevitApp;
			if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

			string modelPath = designAutomationData.FilePath;
			if (String.IsNullOrWhiteSpace(modelPath)) throw new InvalidDataException(nameof(modelPath));

			Document doc = designAutomationData.RevitDoc;
			if (doc == null) throw new InvalidOperationException("Could not open document.");

			List<Coordinates> coordinates = GetCoordinates("coordinates.csv");

			InputParams inputParameters = JsonConvert.DeserializeObject<InputParams>(File.ReadAllText("params.json"));

			dynamic urnResult = new JObject();
			try
			{
				//urnResult.projectBasePoint = BasePoint.GetProjectBasePoint(doc);
				BasePoint pbp = BasePoint.GetProjectBasePoint(doc);
				Console.WriteLine($"Project base point acquired!");
				Console.WriteLine(pbp.Position.ToString());
				urnResult.basePoint = pbp.Position.ToString();
				BasePoint sp = BasePoint.GetSurveyPoint(doc);
				Console.WriteLine($"Survey point acquired!");
				Console.WriteLine(sp.Position.ToString());
				urnResult.surveyPoint = sp.Position.ToString();
				double pp = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero).Angle;
				Console.WriteLine($"True north angle acquired!");
				Console.WriteLine(pp);
				urnResult.trueNorthAngle = pp;
				string fileName = doc.Title;

				try
				{
					Coordinates correctCoordinates = coordinates.Find(c => fileName.Contains(c.code));
					urnResult.correctProjectBasePoint = correctCoordinates.basePoint.ToString();
					urnResult.correctSurveyPoint = correctCoordinates.surveyPoint.ToString();
					if (coordinatesMismatch(correctCoordinates, sp, pbp, inputParameters.tolerance))
					{
						createIssue(inputParameters, correctCoordinates, sp, pbp, fileName);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			// save all to a .json file
			using (StreamWriter file = File.CreateText("result.json"))
			using (JsonTextWriter writer = new JsonTextWriter(file))
			{
				urnResult.WriteTo(writer);
			}
		}

		private void createIssue(InputParams inputParameters, Coordinates correctCoordinates, BasePoint sp, BasePoint pbp, string fileName)
		{
			dynamic body = new JObject();
			body.title = $"{fileName} Coordinates issue";
			body.description = $"file: {fileName}; {Environment.NewLine} " +
				$"urn: {inputParameters.versionUrn}; {Environment.NewLine} " +
				$"project_base_point: {pbp.Position.ToString()}; {Environment.NewLine} " +
				$"project_survey_point: {sp.Position.ToString()}; {Environment.NewLine} " +
				$"correct_base_point: {correctCoordinates.basePoint.ToString()}; {Environment.NewLine} " +
				$"correct_survey_point: {correctCoordinates.surveyPoint.ToString()}; {Environment.NewLine} ";
			body.status = "open";
			body.issueSubtypeId = inputParameters.issueSubTypeId;
			body.assignedTo = inputParameters.userId;
			body.assignedToType = "user";
			body.published = true;


			RestClient client = new RestClient("https://developer.api.autodesk.com/");
			RestRequest request = new RestRequest($"construction/issues/v1/projects/{inputParameters.projectId}/issues", Method.Post);
			request.AddHeader("Authorization", "Bearer " + inputParameters.token);
			request.AddHeader("Content-Type", "application/json");
			string stringBody = JsonConvert.SerializeObject(body);
			request.AddParameter("application/json", stringBody, ParameterType.RequestBody);

			var result = client.ExecuteAsync(request).GetAwaiter().GetResult();
			Console.WriteLine(result);
		}

		private bool coordinatesMismatch(Coordinates correctCoordinates, BasePoint sp, BasePoint pbp, double tolerance)
		{
			double basePointDistance = pbp.Position.DistanceTo(correctCoordinates.basePoint);
			double surveyPointDistance = sp.Position.DistanceTo(correctCoordinates.surveyPoint);

			return (basePointDistance > tolerance || surveyPointDistance > tolerance);
		}

		private static List<Coordinates> GetCoordinates(string fileName)
		{
			List<Coordinates> coordinates = new List<Coordinates>();
			using (var reader = new StreamReader(fileName))
			{
				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					var values = line.Split(',');

					XYZ projectBasePoint = new XYZ(
						double.Parse(values[1]),
						double.Parse(values[2]),
						double.Parse(values[3])
					);

					XYZ surveyPoint = new XYZ(
						double.Parse(values[5]),
						double.Parse(values[6]),
						double.Parse(values[7])
					);

					coordinates.Add(new Coordinates()
					{
						code = values[0],
						angle = double.Parse(values[4]),
						basePoint = projectBasePoint,
						surveyPoint = surveyPoint
					});
				}
			}
			return coordinates;
		}
	}

	internal class InputParams
	{
		public string userId { get; set; }
		public string versionUrn { get; set; }
		public string projectId { get; set; }
		public string hubId { get; set; }
		public double tolerance { get; set; }
		public string token { get; set; }
		public string issueSubTypeId { get; set; }
	}

	public class Coordinates
	{
		public XYZ basePoint { get; set; }
		public XYZ surveyPoint { get; set; }
		public double angle { get; set; }
		public string code { get; set; }
	}
}
