
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using XboxOneServiceAWS.Models;

namespace XboxOneServiceAWS
{
    public class DelegateCommand : ICommand
    {
        private readonly Predicate<object> _canExecute;
        private readonly Action<string> _execute;

        public DelegateCommand(Predicate<object> canExecute, Action<string> execute)
        {
            _canExecute = canExecute;
            _execute = execute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute((string)parameter);
        }

        public event EventHandler CanExecuteChanged;
    }

    public class MainPageViewModel
    {
        private static readonly string AWS_ACCESS_KEY_ID = "***";
        private static readonly string AWS_SECRET_KEY = "***";
        private static readonly string ASSOCIATE_TAG = "***";
        private static readonly string ENDPOINT = "webservices.amazon.es";

        private XmlDocument _doc;

        private string _selectedCategory;

        public ObservableCollection<TitleItem> Values { get; }

        public ICommand Search { get; set; }
        public ICommand SetCategory { get; set; }


        public MainPageViewModel()
        {
            Values = new ObservableCollection<TitleItem>();

            Search = new DelegateCommand(x => true, SearchInAmazon);
            SetCategory = new DelegateCommand(x=>true, SetCategorySearch);
        }

        private void SetCategorySearch(string category)
        {
            _selectedCategory = category;
        }

        private void SearchInAmazon(string keywords)
        {
            GetQueryUrl(keywords)
                .ContinueWith(RunQuery)
                .ContinueWith(x => ParseResults(x.Result))
                .ContinueWith(x => ShowValues(x.Result),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.FromCurrentSynchronizationContext())
                    ;

        }

        private async Task SaveXml()
        {
            if (_doc == null) return;

            var storage = ApplicationData.Current.LocalFolder;
            var storageFile = await storage.CreateFileAsync("result.xml", CreationCollisionOption.ReplaceExisting);

            FileIO.WriteTextAsync(storageFile, _doc.GetXml());
        }

        private async Task<string> GetQueryUrl(string keywords)
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
                {"SearchIndex", _selectedCategory},
                {"ResponseGroup", "Images,ItemAttributes"},
                {"Sort", "salesrank"},
                //{"Keywords", keywords},
                {"Title", keywords }
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

        private async Task<IList<TitleItem>> ParseResults(Task<XmlDocument> results)
        {
            var returnValues = new List<TitleItem>();

            var doc = results.Result;
            _doc = doc;

            var errors = doc.GetElementsByTagName("Message");
            if (errors != null && errors.Count > 0)
                Debug.WriteLine($"ERRORS: {errors}");


            var xmlItems = doc.GetElementsByTagName("Item");
            foreach (var xmlItem in xmlItems)
            {
                var item = CreateTitleItem(xmlItem);

                returnValues.Add(item);
            }

            //var titles = doc.GetElementsByTagName("Title");
            //foreach (var title in titles)
            //{
            //    var value = new TitleItem
            //    {
            //        Name = title.InnerText,
            //        ImageUrl = "image"
            //    };

            //    returnValues.Add(value);
            //}

            return returnValues;
        }

        private TitleItem CreateTitleItem(IXmlNode xmlItem)
        {
            var titleItem = new TitleItem();

            var titleNode =
                xmlItem.ChildNodes.FirstOrDefault(x => x.NodeName.Equals("ItemAttributes"))?
                    .ChildNodes.FirstOrDefault(t => t.NodeName.Equals("Title"));
            titleItem.Name = titleNode?.InnerText;

            var titleImage = xmlItem.ChildNodes.FirstOrDefault(x=>x.NodeName.Equals("MediumImage"))?.FirstChild;
            titleItem.ImageUrl = titleImage?.InnerText;

            return titleItem;
        }

        private async Task ShowValues(Task<IList<TitleItem>> values)
        {

            SaveXml();

            Values.Clear();

            foreach (var value in values.Result)
            {
                Values.Add(value);
            }
        }

    }
}
