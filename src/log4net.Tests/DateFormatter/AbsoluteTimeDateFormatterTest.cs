#region Apache License
//
// Licensed to the Apache Software Foundation (ASF) under one or more 
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership. 
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with 
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Globalization;
using System.IO;
using System.Text;
using log4net.DateFormatter;
using NUnit.Framework;

namespace log4net.Tests.DateFormatter;

[TestFixture]
public class AbsoluteTimeDateFormatterTest
{
  [TearDown]
  public void ResetCounts() => FormatterOne.Invocations = 0;

  [Test]
  public void CacheWorksForSameTicks()
  {
    using StringWriter sw = new();
    FormatterOne f1 = new();
    FormatterOne f2 = new();
    DateTime dt = DateTime.Now;
    f1.FormatDate(dt, sw);
    f2.FormatDate(dt, sw);
    Assert.That(FormatterOne.Invocations, Is.EqualTo(1));
  }

  [Test]
  public void CacheWorksForSameSecond()
  {
    using StringWriter sw = new();
    FormatterOne f1 = new();
    FormatterOne f2 = new();
    DateTime dt1 = DateTime.Today;
    DateTime dt2 = dt1.AddMilliseconds(600);
    f1.FormatDate(dt1, sw);
    f2.FormatDate(dt2, sw);
    Assert.That(FormatterOne.Invocations, Is.EqualTo(1));
  }

  [Test]
  public void CacheExpiresWhenCrossingSecond()
  {
    using StringWriter sw = new();
    FormatterOne f1 = new();
    FormatterOne f2 = new();
    DateTime dt1 = DateTime.Today.AddMinutes(1);
    DateTime dt2 = dt1.AddMilliseconds(1100);
    f1.FormatDate(dt1, sw);
    f2.FormatDate(dt2, sw);
    Assert.That(FormatterOne.Invocations, Is.EqualTo(2));
  }

  [Test]
  public void CacheIsLocalToSubclass()
  {
    using StringWriter sw = new();
    FormatterOne f1 = new();
    FormatterTwo f2 = new();
    DateTime dt1 = DateTime.Today.AddMinutes(10);
    f1.FormatDate(dt1, sw);
    f2.FormatDate(dt1, sw);
    Assert.That(FormatterOne.Invocations, Is.EqualTo(2));
  }

  [Test]
  public void TestFormattingResults()
  {
    AbsoluteTimeDateFormatter formatter = new();
    StringBuilder sb = new();
    using StringWriter writer = new(sb, CultureInfo.InvariantCulture);

    // Tests for prepended 0 characters for 2-digit and 3-digit portions.
    formatter.FormatDate(new DateTime(1970, 1, 1, 1, 1, 1).AddMilliseconds(1), writer);
    Assert.That(sb.ToString(), Is.EqualTo("01:01:01,001"));
    sb.Clear();

    // Non-zero-prepend case.
    formatter.FormatDate(new DateTime(2100, 12, 30, 11, 59, 59).AddMilliseconds(100), writer);
    Assert.That(sb.ToString(), Is.EqualTo("11:59:59,100"));
    sb.Clear();
  }
}

internal class FormatterOne : AbsoluteTimeDateFormatter
{
  internal static int Invocations;

  protected override void FormatDateWithoutMillis(DateTime dateToFormat, StringBuilder buffer) => Invocations++;
}

internal sealed class FormatterTwo : FormatterOne
{ }
