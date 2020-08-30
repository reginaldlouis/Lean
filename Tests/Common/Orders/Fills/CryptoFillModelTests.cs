/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using NUnit.Framework;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;
using QuantConnect.Securities.Forex;
using QuantConnect.Tests.Common.Data;
using QuantConnect.Tests.Common.Securities;

namespace QuantConnect.Tests.Common.Orders.Fills
{
    [TestFixture]
    public class CryptoFillModelTests
    {
        private static readonly DateTime Noon = new DateTime(2014, 6, 24, 12, 0, 0);
        private static readonly TimeKeeper TimeKeeper = new TimeKeeper(Noon.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);

        [Test]
        public void PerformsMarketFillBuy()
        {
            var model = new CryptoFillModel();
            var order = new MarketOrder(Symbols.BTCUSD, 100, Noon);
            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(0, 0, 0, 101.123m), 100,
                new Bar(0, 0, 0, 101.123m), 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(security.Price, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsMarketFillSell()
        {
            var model = new CryptoFillModel();
            var order = new MarketOrder(Symbols.BTCUSD, -100, Noon);
            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 100,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(security.Price, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsLimitFillBuy()
        {
            var model = new CryptoFillModel();
            var order = new LimitOrder(Symbols.BTCUSD, 100, 101.5m, Noon);
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var configTradeBar = new SubscriptionDataConfig(configQuoteBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configTradeBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(102m, 102m, 102m, 102m), 100,
                new Bar(102m, 102m, 102m, 102m), 100));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            // Should use QuoteBar since it has a newer timestamp despite being added first in this setup
            var quoteBar = new QuoteBar(Noon.AddMinutes(1), Symbols.BTCUSD,
                new Bar(101m, 102m, 100m, 101.3m), 100,
                new Bar(103m, 104m, 102m, 103.3m), 100);
            security.SetMarketPrice(quoteBar);
            security.SetMarketPrice(new TradeBar(Noon, Symbols.BTCUSD, 101.4m, 101.4m, 101m, 101.4m, 100));

            fill = model.LimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(Math.Min(order.LimitPrice, quoteBar.High), fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsLimitFillSell()
        {
            var model = new CryptoFillModel();
            var order = new LimitOrder(Symbols.BTCUSD, -100, 101.5m, Noon);
            var configTradeBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configTradeBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101m, 101m, 101m, 101m), 101m,
                new Bar(101m, 101m, 101m, 101m), 101m));

            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            var quoteBar = new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101.6m, 102m, 101.6m, 101.6m), 100,
                new Bar(103m, 104m, 102m, 103.3m), 100);
            security.SetMarketPrice(quoteBar);
            security.SetMarketPrice(new TradeBar(Noon, Symbols.BTCUSD, 102m, 103m, 101.5m, 102.3m, 100));

            fill = model.LimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(Math.Max(order.LimitPrice, quoteBar.Low), fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsStopLimitFillBuy()
        {
            var model = new CryptoFillModel();
            var order = new StopLimitOrder(Symbols.BTCUSD, 100, 101.5m, 101.75m, Noon);
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(100m, 100m, 100m, 100m), 100,
                new Bar(100m, 100m, 100m, 100m), 100));

            var configTradeBar = new SubscriptionDataConfig(configQuoteBar, typeof(TradeBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configTradeBar);
            configProvider.SubscriptionDataConfigs.Add(configQuoteBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon.AddMinutes(1), Symbols.BTCUSD,
                new Bar(102m, 102m, 102m, 102m), 100,
                new Bar(102m, 102m, 102m, 102m), 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon.AddMinutes(2), Symbols.BTCUSD,
                new Bar(101m, 102m, 100m, 100.66m), 100,
                new Bar(103m, 104m, 102m, 102.66m), 100));
            security.SetMarketPrice(new TradeBar(Noon.AddMinutes(3), Symbols.BTCUSD, 102m, 103m, 101m, 101.66m, 100));

            fill = model.StopLimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(Math.Min(security.High, order.LimitPrice), fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsStopLimitFillSell()
        {
            var model = new CryptoFillModel();
            var order = new StopLimitOrder(Symbols.BTCUSD, -100, 101.75m, 101.50m, Noon);
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(102m, 102m, 102m, 102m), 100,
                new Bar(102m, 102m, 102m, 102m), 100));

            var configTradeBar = new SubscriptionDataConfig(configQuoteBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configTradeBar);
            configProvider.SubscriptionDataConfigs.Add(configQuoteBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon.AddMinutes(1), Symbols.BTCUSD,
                new Bar(101m, 101m, 101m, 101m), 100,
                new Bar(101m, 101m, 101m, 101m), 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon.AddMinutes(2), Symbols.BTCUSD,
                new Bar(101m, 102m, 100m, 100.66m), 100,
                new Bar(103m, 104m, 102m, 102.66m), 100));
            security.SetMarketPrice(new TradeBar(Noon.AddMinutes(3), Symbols.BTCUSD, 102m, 103m, 101m, 101.66m, 100));

            fill = model.StopLimitFill(security, order);

            // this fills worst case scenario, so it's at the limit price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(Math.Max(security.Low, order.LimitPrice), fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsStopMarketFillBuy()
        {
            var model = new CryptoFillModel();
            var order = new StopMarketOrder(Symbols.BTCUSD, 100, 101.5m, Noon);
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101m, 101m, 101m, 101m), 100,
                new Bar(101m, 101m, 101m, 101m), 100));

            var configTradeBar = new SubscriptionDataConfig(configQuoteBar, typeof(QuoteBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configTradeBar);
            configProvider.SubscriptionDataConfigs.Add(configQuoteBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101m, 102m, 100m, 101.5m), 100,
                new Bar(103m, 104m, 102m, 103.5m), 100));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.BTCUSD, 102m, 103m, 101m, 102.5m, 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            // this fills worst case scenario, so it's min of asset/stop price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(security.AskPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsStopMarketFillSell()
        {
            var model = new CryptoFillModel();
            var order = new StopMarketOrder(Symbols.BTCUSD, -100, 101.5m, Noon);
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(102m, 102m, 102m, 102m), 100,
                new Bar(102m, 102m, 102m, 102m), 100));

            var configTradeBar = new SubscriptionDataConfig(configQuoteBar, typeof(TradeBar));
            var configProvider = new MockSubscriptionDataConfigProvider(configTradeBar);
            configProvider.SubscriptionDataConfigs.Add(configQuoteBar);

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101m, 102m, 100m, 100m), 100,
                new Bar(103m, 104m, 102m, 102m), 100));
            security.SetMarketPrice(new TradeBar(Noon, Symbols.BTCUSD, 102m, 103m, 101m, 101m, 100));

            fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            // this fills worst case scenario, so it's min of asset/stop price
            Assert.AreEqual(order.Quantity, fill.FillQuantity);
            Assert.AreEqual(security.BidPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
        }

        [Test]
        public void PerformsMarketOnOpenUsingOpenPrice()
        {
            var reference = new DateTime(2015, 06, 05, 9, 0, 0); // before market open
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            var time = reference;
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(time, Symbols.BTCUSD,
                new Bar(1m, 2m, 0.5m, 1.33m), 100,
                new Bar(1m, 2m, 0.5m, 1.33m), 100));

            Assert.Throws<NotSupportedException>(() =>
                new CryptoFillModel().Fill(
                    new FillModelParameters(
                        security,
                        new MarketOnOpenOrder(Symbols.BTCUSD, 100, reference),
                        new MockSubscriptionDataConfigProvider(configQuoteBar),
                        Time.OneHour
                    )
                )
            );
        }

        [Test]
        public void PerformsMarketOnCloseUsingClosingPrice()
        {
            var reference = new DateTime(2015, 06, 05, 15, 0, 0); // before market close
            var configQuoteBar = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            var time = reference;
            TimeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(time - configQuoteBar.Increment, Symbols.BTCUSD,
                new Bar(1m, 2m, 0.5m, 1.33m), 100,
                new Bar(1m, 2m, 0.5m, 1.33m), 100, configQuoteBar.Increment));

            Assert.Throws<NotSupportedException>(()=>
                new CryptoFillModel().Fill(
                    new FillModelParameters(
                        security,
                        new MarketOnCloseOrder(Symbols.BTCUSD, 100, reference),
                        new MockSubscriptionDataConfigProvider(configQuoteBar),
                        Time.OneHour
                    )
                )
            );
        }

        [TestCase(OrderDirection.Buy)]
        [TestCase(OrderDirection.Sell)]
        public void MarketOrderFillsAtBidAsk(OrderDirection direction)
        {
            var symbol = Symbol.Create("EURUSD", SecurityType.Forex, "fxcm");
            var exchangeHours = SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork);
            var quoteCash = new Cash(Currencies.USD, 1000, 1);
            var symbolProperties = SymbolProperties.GetDefault(Currencies.USD);
            var config = new SubscriptionDataConfig(typeof(Tick), symbol, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = new Forex(exchangeHours, quoteCash, config, symbolProperties, ErrorCurrencyConverter.Instance, RegisteredSecurityDataTypesProvider.Null);

            var reference = DateTime.Now;
            var referenceUtc = reference.ConvertToUtc(TimeZones.NewYork);
            var timeKeeper = new TimeKeeper(referenceUtc);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var brokerageModel = new FxcmBrokerageModel();
            var fillModel = brokerageModel.GetFillModel(security);

            const decimal bidPrice = 1.13739m;
            const decimal askPrice = 1.13746m;

            security.SetMarketPrice(new Tick(DateTime.Now, symbol, bidPrice, askPrice));

            var quantity = direction == OrderDirection.Buy ? 1 : -1;
            var order = new MarketOrder(symbol, quantity, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            var expected = direction == OrderDirection.Buy ? askPrice : bidPrice;
            Assert.AreEqual(expected, fill.FillPrice);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [Test]
        public void ImmediateFillModelUsesPriceForTicksWhenBidAskSpreadsAreNotAvailable()
        {
            var noon = new DateTime(2014, 6, 24, 12, 0, 0);
            var timeKeeper = new TimeKeeper(noon.ConvertToUtc(TimeZones.NewYork), new[] { TimeZones.NewYork });
            var config = new SubscriptionDataConfig(typeof(Tick), Symbols.BTCUSD, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new TradeBar(noon, Symbols.BTCUSD, 101.123m, 101.123m, 101.123m, 101.123m, 100));

            // Add both a quotebar and a tick to the security cache
            // This is the case when a tick is seeded with minute data in an algorithm
            security.Cache.AddData(new QuoteBar(DateTime.MinValue, Symbols.BTCUSD,
                new Bar(1.0m, 1.0m, 1.0m, 1.0m), 1.0m,
                new Bar(1.0m, 1.0m, 1.0m, 1.0m), 1.0m));
            security.Cache.AddData(new Tick(config, "42525000,100,10,100,10,0", DateTime.MinValue));

            var fillModel = new CryptoFillModel();
            var order = new MarketOrder(Symbols.BTCUSD, 1000, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            // The fill model should use the tick.Price
            Assert.AreEqual(fill.FillPrice, 100m);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [Test]
        public void ImmediateFillModelDoesNotUseTicksWhenThereIsNoTickSubscription()
        {
            var noon = new DateTime(2014, 6, 24, 12, 0, 0);
            var timeKeeper = new TimeKeeper(noon.ConvertToUtc(TimeZones.NewYork), new[] { TimeZones.NewYork });
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);
            // Minute subscription
            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(noon, Symbols.BTCUSD,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 100,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 100));

            // This is the case when a tick is seeded with minute data in an algorithm
            security.Cache.AddData(new QuoteBar(DateTime.MinValue, symbol,
                new Bar(1.0m, 1.0m, 1.0m, 1.0m), 1.0m,
                new Bar(1.0m, 1.0m, 1.0m, 1.0m), 1.0m));
            security.Cache.AddData(new Tick(config, "42525000,1000000,100,A,@,0", DateTime.MinValue));

            var fillModel = new CryptoFillModel();
            var order = new MarketOrder(symbol, 1000, DateTime.Now);
            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            // The fill model should use the tick.Price
            Assert.AreEqual(fill.FillPrice, 1.0m);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 290.50)]
        [TestCase(-100, 291.50)]
        public void LimitOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal limitPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var config = CreateQuoteBarConfig(symbol);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var quoteBar = new QuoteBar(time, symbol,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (QuoteBar)quoteBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new CryptoFillModel();
            var order = new LimitOrder(symbol, orderQuantity, limitPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            quoteBar = new QuoteBar(time, symbol,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            fill = fillModel.LimitFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(limitPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 291.50)]
        [TestCase(-100, 290.50)]
        public void StopMarketOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal stopPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);

            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var quoteBar = new QuoteBar(time, Symbols.BTCUSD,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (QuoteBar)quoteBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new CryptoFillModel();
            var order = new StopMarketOrder(Symbols.BTCUSD, orderQuantity, stopPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            quoteBar = new QuoteBar(time, Symbols.BTCUSD,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            fill = fillModel.StopMarketFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(stopPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 291.50, 291.75)]
        [TestCase(-100, 290.50, 290.25)]
        public void StopLimitOrderDoesNotFillUsingDataBeforeSubmitTime(decimal orderQuantity, decimal stopPrice, decimal limitPrice)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);

            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var quoteBar = new QuoteBar(time, Symbols.BTCUSD,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            var fillForwardBar = (QuoteBar)quoteBar.Clone(true);
            security.SetMarketPrice(fillForwardBar);

            var fillModel = new CryptoFillModel();
            var order = new StopLimitOrder(Symbols.BTCUSD, orderQuantity, stopPrice, limitPrice, time.ConvertToUtc(TimeZones.NewYork));

            var fill = fillModel.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            time += TimeSpan.FromMinutes(1);
            timeKeeper.SetUtcDateTime(time.ConvertToUtc(TimeZones.NewYork));

            quoteBar = new QuoteBar(time, Symbols.BTCUSD,
                new Bar(290m, 292m, 289m, 291m), 12345,
                new Bar(290m, 292m, 289m, 291m), 12345);
            security.SetMarketPrice(quoteBar);

            fill = fillModel.StopLimitFill(security, order);

            Assert.AreEqual(orderQuantity, fill.FillQuantity);
            Assert.AreEqual(limitPrice, fill.FillPrice);
            Assert.AreEqual(OrderStatus.Filled, fill.Status);
            Assert.AreEqual(0, fill.OrderFee.Value.Amount);
        }

        [TestCase(100, 105)]
        [TestCase(-100, 100)]
        public void StopMarketOrderDoesNotFillWithOpenInterest(decimal orderQuantity, decimal openInterest)
        {
            var model = new CryptoFillModel();
            var order = new StopMarketOrder(Symbols.BTCUSD, orderQuantity, 101.5m, Noon);
            var configTick = new SubscriptionDataConfig(typeof(Tick), Symbols.BTCUSD, Resolution.Tick, TimeZones.NewYork, TimeZones.NewYork, true, true, false);
            var security = CreateSecurity(configTick);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var configOpenInterest = new SubscriptionDataConfig(configTick, typeof(OpenInterest));
            var configProvider = new MockSubscriptionDataConfigProvider(configOpenInterest);
            configProvider.SubscriptionDataConfigs.Add(configTick);

            security.SetMarketPrice(new Tick(Noon, Symbols.BTCUSD, 101.5m, 101.5m, 101.5m));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                configProvider,
                Time.OneHour)).OrderEvent;

            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);

            security.Update(new[] { new OpenInterest(Noon, Symbols.BTCUSD, openInterest) }, typeof(Tick));

            fill = model.StopMarketFill(security, order);

            // Should not fill
            Assert.AreEqual(0, fill.FillQuantity);
            Assert.AreEqual(0, fill.FillPrice);
            Assert.AreEqual(OrderStatus.None, fill.Status);
        }

        [Test]
        public void MarketOrderFillWithStalePriceHasWarningMessage()
        {
            var model = new CryptoFillModel();
            var order = new MarketOrder(Symbols.BTCUSD, -100, Noon.ConvertToUtc(TimeZones.NewYork).AddMinutes(61));
            var config = CreateQuoteBarConfig(Symbols.BTCUSD);
            var security = CreateSecurity(config);
            security.SetLocalTimeKeeper(TimeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));
            security.SetMarketPrice(new QuoteBar(Noon, Symbols.BTCUSD,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 0,
                new Bar(101.123m, 101.123m, 101.123m, 101.123m), 0, TimeSpan.Zero));

            var fill = model.Fill(new FillModelParameters(
                security,
                order,
                new MockSubscriptionDataConfigProvider(config),
                Time.OneHour)).OrderEvent;

            Assert.IsTrue(fill.Message.Contains("Warning: fill at stale price"));
        }

        [TestCase(OrderDirection.Sell, 11)]
        [TestCase(OrderDirection.Buy, 21)]
        // uses the trade bar last close
        [TestCase(OrderDirection.Hold, 291)]
        public void PriceReturnsQuoteBarsIfPresent(OrderDirection orderDirection, decimal expected)
        {
            var time = new DateTime(2018, 9, 24, 9, 30, 0);
            var timeKeeper = new TimeKeeper(time.ConvertToUtc(TimeZones.NewYork), TimeZones.NewYork);
            var symbol = Symbol.Create("SPY", SecurityType.Equity, Market.USA);

            var configTradeBar = CreateQuoteBarConfig(symbol);
            var configQuoteBar = new SubscriptionDataConfig(configTradeBar, typeof(QuoteBar));
            var security = CreateSecurity(configQuoteBar);
            security.SetLocalTimeKeeper(timeKeeper.GetLocalTimeKeeper(TimeZones.NewYork));

            var tradeBar = new TradeBar(time, symbol, 290m, 292m, 289m, 291m, 12345);
            security.SetMarketPrice(tradeBar);

            var quoteBar = new QuoteBar(time, symbol,
                new Bar(10, 15, 5, 11),
                100,
                new Bar(20, 25, 15, 21),
                100);
            security.SetMarketPrice(quoteBar);

            var configProvider = new MockSubscriptionDataConfigProvider(configQuoteBar);
            configProvider.SubscriptionDataConfigs.Add(configTradeBar);

            var testFillModel = new TestFillModel();
            testFillModel.SetParameters(new FillModelParameters(security,
                null,
                configProvider,
                TimeSpan.FromDays(1)));

            //var result = testFillModel.GetPricesPublic(security, orderDirection);

            //Assert.AreEqual(expected, result.Current);
        }

        private SubscriptionDataConfig CreateQuoteBarConfig(Symbol symbol)
        {
            return new SubscriptionDataConfig(typeof(QuoteBar), symbol, Resolution.Minute, TimeZones.EasternStandard, TimeZones.NewYork, true, true, false);
        }

        private Security CreateSecurity(SubscriptionDataConfig config)
        {
            return new Security(
                SecurityExchangeHoursTests.CreateForexSecurityExchangeHours(),
                config,
                new Cash(Currencies.USD, 0, 1m),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache()
            );
        }

        private class TestFillModel : FillModel
        {
            public void SetParameters(FillModelParameters parameters)
            {
                Parameters = parameters;
            }
        }
    }
}
