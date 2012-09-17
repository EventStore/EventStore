// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

namespace EventStore.Core.Tests.Infrastructure.Services.GossipService
{
    class gossip_service_should
    {

        //[Test]
        //public void UpdateListOfKnownBoxesAndSendMergedInformationBackWhenRequestForKnownBoxesReceived()
        //{
        //    var now = DateTime.UtcNow;

        //    var boxAOld = new BoxInfo("box_A", now.AddMinutes(-5));
        //    var boxANew = new BoxInfo("box_A", now.AddMinutes(-1));

        //    var boxB = new BoxInfo("box_B", now.AddMinutes(-7));
        //    var boxC = new BoxInfo("box_C", now.AddMinutes(-2));

        //    var boxDOld = new BoxInfo("box_D", now.AddMinutes(-10));
        //    var boxDNew = new BoxInfo("box_D", now.AddMinutes(-3));

        //    var boxE = new BoxInfo("box_E", now.AddMinutes(-2));

        //    var initialKnownBoxes = new List<BoxInfo> { boxAOld, boxDNew, boxE };
        //    Announcements.BoxData.KnownBoxes.AddRange(initialKnownBoxes);

        //    var externalNodeKnownBoxes = new List<BoxInfo> { boxANew, boxB, boxC, boxDOld };
        //    Publish(new BoxMessage.BoxesInformationRequest(externalNodeKnownBoxes));

        //    var genericMessage = BoxMessenger.Messages.Last();
        //    var responseWithUpdatedListofKnownBoxes = (BoxMessage.BoxesInformationResponse)genericMessage;

        //    var expectedBoxes = new List<BoxInfo> { boxANew, boxB, boxC, boxDNew, boxE };
        //    CollectionAssert.AreEquivalent(expectedBoxes, responseWithUpdatedListofKnownBoxes.Boxes);
        //}

        //[Test]
        //public void UpdateListOfKnownBoxesWhenResponseForAnnouncementReceived()
        //{
        //    var now = DateTime.UtcNow;

        //    var boxAOld = new BoxInfo("box_A", now.AddMinutes(-5));
        //    var boxANew = new BoxInfo("box_A", now.AddMinutes(-1));

        //    var boxB = new BoxInfo("box_B", now.AddMinutes(-7));
        //    var boxC = new BoxInfo("box_C", now.AddMinutes(-2));

        //    var boxDOld = new BoxInfo("box_D", now.AddMinutes(-10));
        //    var boxDNew = new BoxInfo("box_D", now.AddMinutes(-3));

        //    var boxE = new BoxInfo("box_E", now.AddMinutes(-2));


        //    var initialKnownBoxes = new List<BoxInfo> { boxAOld, boxDNew, boxE };
        //    Announcements.BoxData.KnownBoxes.AddRange(initialKnownBoxes);

        //    var externalNodeKnownBoxes = new List<BoxInfo> { boxANew, boxB, boxC, boxDOld };
        //    Publish(new BoxMessage.BoxesInformationRequest(externalNodeKnownBoxes));

        //    var expectedBoxes = new List<BoxInfo> { boxANew, boxB, boxC, boxDNew, boxE };

        //    CollectionAssert.AreEquivalent(expectedBoxes, Announcements.BoxData.KnownBoxes);
        //}

        //[Test]
        //public void UpdateListOfKnownBoxesWhenBoxesInformationResponseMessageReceived()
        //{
        //    var currentlyKnownBoxes = new List<BoxInfo>
        //                                  {
        //                                      new BoxInfo("box_A", DateTime.UtcNow.AddMinutes(-5)),
        //                                      new BoxInfo("box_B", DateTime.UtcNow.AddMinutes(-7))
        //                                  };
        //    var receivedKnownBoxes = new List<BoxInfo>
        //                                  {
        //                                      new BoxInfo("box_A", DateTime.UtcNow.AddMinutes(-1)),
        //                                      new BoxInfo("box_B", DateTime.UtcNow.AddMinutes(-3)),
        //                                      new BoxInfo("box_C", DateTime.UtcNow.AddMinutes(-5))
        //                                  };

        //    Announcements.BoxData.KnownBoxes.AddRange(currentlyKnownBoxes);

        //    Publish(new BoxMessage.BoxesInformationResponse(receivedKnownBoxes));

        //    CollectionAssert.AreEquivalent(receivedKnownBoxes, Announcements.BoxData.KnownBoxes);
        //}
    }
}
