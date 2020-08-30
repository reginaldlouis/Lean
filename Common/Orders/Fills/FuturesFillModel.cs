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

using QuantConnect.Securities;
using System;

namespace QuantConnect.Orders.Fills
{
    /// <summary>
    /// Implements <see cref="IFillModel"/> for Futures
    /// </summary>
    public class FuturesFillModel : EquityFillModel
    {
        /// <summary>
        /// Market fill model for the Futures. Fills at the last bid or ask price.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketFill(Security asset, MarketOrder order)
        {
            return EnsureMinimumPriceVariation(base.MarketFill(asset, order), asset);
        }

        /// <summary>
        /// Stop fill model implementation for Futures. Fills at the last bid or ask price.
        /// The order is filled when the security price reaches the stop price.
        /// We model the security price with its High and Low to account intrabar prices.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="MarketFill(Security, MarketOrder)"/>
        public override OrderEvent StopMarketFill(Security asset, StopMarketOrder order)
        {
            return EnsureMinimumPriceVariation(base.StopMarketFill(asset, order), asset);
        }

        /// <summary>
        /// Stop-Limit fill model implementation for Futures.
        /// We model the security price with its High and Low to account intrabar prices.
        /// The order is filled when the security price reaches the limit price and the worse price is assumed.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="StopMarketFill(Security, StopMarketOrder)"/>
        /// <remarks>
        ///     There is no good way to model limit orders with OHLC because we never know whether the market has
        ///     gapped past our fill price. We have to make the assumption of a fluid, high volume market.
        ///
        ///     Stop limit orders we also can't be sure of the order of the H - L values for the limit fill. The assumption
        ///     was made the limit fill will be done with closing price of the bar after the stop has been triggered..
        /// </remarks>
        public override OrderEvent StopLimitFill(Security asset, StopLimitOrder order)
        {
            return EnsureMinimumPriceVariation(base.StopLimitFill(asset, order), asset);
        }

        /// <summary>
        /// Limit fill model implementation for Futures.
        /// We model the security price with its High and Low to account intrabar prices.
        /// The order is filled when the security price reaches the limit price and the worse price is assumed.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        /// <seealso cref="StopMarketFill(Security, StopMarketOrder)"/>
        /// <seealso cref="MarketFill(Security, MarketOrder)"/>
        public override OrderEvent LimitFill(Security asset, LimitOrder order)
        {
            return EnsureMinimumPriceVariation(base.LimitFill(asset, order), asset);
        }

        /// <summary>
        /// Market on Open fill model for the Futures. Fills with the opening price of the trading session.
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketOnOpenFill(Security asset, MarketOnOpenOrder order)
        {
            return EnsureMinimumPriceVariation(base.MarketOnOpenFill(asset, order), asset);
        }

        /// <summary>
        /// Market on Close fill model for the Futures. Fills with the closing price of the trading session.
        /// </summary>
        /// <param name="asset">Asset we're trading with this order</param>
        /// <param name="order">Order to be filled</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketOnCloseFill(Security asset, MarketOnCloseOrder order)
        {
            return EnsureMinimumPriceVariation(base.MarketOnCloseFill(asset, order), asset);
        }

        /// <summary>
        /// Ensure that the fill price for future orders obey the minimum price variation
        /// </summary>
        /// <param name="fill">Order fill information detailing the average price and quantity filled.</param>
        /// <param name="asset">Asset we're trading with this fill</param>
        /// <returns></returns>
        protected OrderEvent EnsureMinimumPriceVariation(OrderEvent fill, Security asset)
        {
            if (!fill.Status.IsFill())
            {
                return fill;
            }

            var increment = asset.PriceVariationModel.GetMinimumPriceVariation(
                new GetMinimumPriceVariationParameters(asset, fill.FillPrice));

            if (increment == 0)
            {
                return fill;
            }

            var normalizedPrice = fill.FillPrice / increment;
            var roundedNormalizedPrice = Math.Round(normalizedPrice);
            if (roundedNormalizedPrice == normalizedPrice)
            {
                return fill;
            }

            // Add 1 to round UP for sell order
            if (fill.Direction == OrderDirection.Sell) roundedNormalizedPrice += 1;

            // Multiply by minimum price variation to set the price with this variation
            fill.FillPrice = roundedNormalizedPrice * increment;

            return fill;
        }
    }
}