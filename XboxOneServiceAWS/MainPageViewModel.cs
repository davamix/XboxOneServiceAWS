using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;

namespace XboxOneServiceAWS
{
    public class MainPageViewModel
    {
		private static readonly string AWS_ACCESS_KEY_ID = "***";
		private static readonly string AWS_SECRET_KEY = "***";
	    private static readonly string ASSOCIATE_TAG = "***";
		private static readonly string ENDPOINT = "webservices.amazon.es";

		public ObservableCollection<string> Values { get; }

        public MainPageViewModel()
        {
            Values = new ObservableCollection<string>();

	        GetQueryUrl()
		        .ContinueWith(RunQuery)
		        .ContinueWith(x => ParseResults(x.Result))
		        .ContinueWith(x => ShowValues(x.Result),
			        CancellationToken.None,
			        TaskContinuationOptions.OnlyOnRanToCompletion,
			        TaskScheduler.FromCurrentSynchronizationContext());

        }

		private async Task<string> GetQueryUrl()
		{
			SignedRequestHelper signedHelper;

			try
			{
				signedHelper = new SignedRequestHelper(AWS_ACCESS_KEY_ID, AWS_SECRET_KEY, ENDPOINT);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				return string.Empty;
			}

			var parameters = new Dictionary<string, string>
			{
				{"Service", "AWSECommerceService"},
				{"Operation", "ItemSearch"},
				{"AWSAccessKeyId", AWS_ACCESS_KEY_ID},
				{"AssociateTag", ASSOCIATE_TAG},
				{"SearchIndex", "DVD"},
				{"ResponseGroup", "Images,ItemAttributes"},
				{"Sort", "price"},
				{"Keywords", "Toy Story"}
			};

			return signedHelper.Sign(parameters);
		}

		private async Task<XmlDocument> RunQuery(Task<string> url)
		{
			var uri = new Uri(url.Result);
			try
			{
				var doc = await XmlDocument.LoadFromUriAsync(uri);
				return doc;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}

			return null;
		}

		private async Task<IList<string>>  ParseResults(Task<XmlDocument> results)
	    {
		    var returnValues = new List<string>();

		    var doc = results.Result;

		    var errors = doc.GetElementsByTagName("Message");
			if(errors != null && errors.Count > 0)
				Debug.WriteLine($"ERRORS: {errors}");

		    var titles = doc.GetElementsByTagName("Title");
		    foreach (var title in titles)
		    {
			    returnValues.Add(title.InnerText);
		    }

		    return returnValues;
	    }

		private async Task ShowValues(Task<IList<string>> values)
		{
			foreach (var value in values.Result)
			{
				Values.Add(value);
			}
		}
	    
    }
}
