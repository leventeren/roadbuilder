﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrafficNetwork;
using System.Linq;
using MoreLinq;

namespace TrafficParticipant
{
    public partial class VehicleLaneController : MonoBehaviour
    {
        public VehicleLaneController directFollowing, directFollower;

        VehicleLaneController _rightFollowing, _leftFollowing, _rightFollower, _leftFollower;
        public VehicleLaneController rightFollowing
        {
            set
            {
                if (value != _rightFollowing && _rightFollowing != null && _rightFollowing._leftFollower == this)
                {
                    _rightFollowing._leftFollower = null;
                }

                if (value)
                {
                    if (!value.isRightNeighborOf(this, out _, out _) || !isLeftNeighborOf(value, out _, out _))
                    {
                        // fails validation
                        _rightFollowing = null;
                        return;
                    }

                    value._leftFollower = this;
                }
                
                _rightFollowing = value;
            }
            get
            {
                return _rightFollowing;
            }
        }

        public VehicleLaneController leftFollowing
        {
            set
            {
                if (value != _leftFollowing && _leftFollowing != null && _leftFollowing._rightFollower == this)
                {
                    _leftFollowing._rightFollower = null;
                }
                if (value)
                {
                    if (!value.isLeftNeighborOf(this, out _, out _) || !isRightNeighborOf(value, out _, out _))
                    {
                        _leftFollowing = null;
                        return;
                    }

                    value._rightFollower = this;
                }
                
                _leftFollowing = value;
            }
            get
            {
                return _leftFollowing;
            }
        }

        public VehicleLaneController rightFollower
        {
            get
            {
                return _rightFollower;
            }
        }

        public VehicleLaneController leftFollower
        {
            get
            {
                return _leftFollower;
            }
        }

        [HideInInspector]
        public Link linkOn;
        [HideInInspector]
        public int laneOn;
        public float percentageTravelled;
        [HideInInspector]
        public float speed;

        private void Start()
        {
            //TODO: bump detection
            if (linkOn == null)
            {
                Debug.LogError("LinkOn must be set before start!");
            }

            VehicleLaneController frontVehicle = FindFrontVehicleInLane(linkOn, laneOn);
            directFollowing = frontVehicle;
            if (frontVehicle != null)
            {
                frontVehicle.directFollower = this;
            }

            if (laneOn + 1 <= linkOn.maxLane)
            {
                rightFollowing = GetFollowingForNewRightLane();
            }

            if (laneOn - 1 >= linkOn.minLane)
            {
                leftFollowing = GetFollowingForNewLeftLane();
            }

            linkOn.vehicles.Add(this);
            percentageTravelled = 0;
        }

        // distance is real
        public bool Step(float distance)
        {
            if (directFollowing != null) {
                Debug.Assert(distance * distance < (directFollowing.transform.position - transform.position).sqrMagnitude);
            }
            if (percentageTravelled + distance / linkOn.curve.curveLength >= 1.0f)
            {
                bool has_right_neighbor = laneOn != linkOn.maxLane;
                bool has_left_neighbor = laneOn != linkOn.minLane;

                linkOn.vehicles.Remove(this);
                float onOldCurve = linkOn.curve.curveLength * (1.0f - percentageTravelled);
                if (!linkOn.GetNextLink(laneOn, out linkOn, out laneOn))
                {
                    // Path terminated
                    DestroyImmediate(gameObject);
                    return false;
                }
                bool now_has_right_neighbor = laneOn != linkOn.maxLane;
                bool now_has_left_neighbor = laneOn != linkOn.minLane;

                // Enter new link
                linkOn.vehicles.Add(this);
                percentageTravelled = (distance - onOldCurve) / linkOn.curve.curveLength;
                // Travel aross Node: Update neighbors
                if (has_right_neighbor && !now_has_right_neighbor)
                {
                    rightFollowing = null;
                    if (rightFollower)
                    {
                        rightFollower.leftFollowing = null;
                    }
                }
                
                if (!has_right_neighbor && now_has_right_neighbor)
                {
                    rightFollowing = GetFollowingForNewRightLane();
                }

                if (has_left_neighbor && !now_has_left_neighbor)
                {
                    leftFollowing = null;
                    if (leftFollower)
                    {
                        leftFollower.rightFollowing = null;
                    }
                }

                if (!has_left_neighbor && now_has_left_neighbor)
                {
                    leftFollowing = GetFollowingForNewLeftLane();
                }
                
                return true;
            }
            else
            {
                percentageTravelled += distance / linkOn.curve.curveLength;
                float itravelledLinks = linkOn.GetAncestorInfo(laneOn, out _, out _);
                // Handle take over

                if (rightFollowing)
                {
                    float rightTravelledLinks = rightFollowing.linkOn.GetAncestorInfo(rightFollowing.laneOn - 1, out _, out _);

                    if (itravelledLinks + percentageTravelled * linkOn.curve.curveLength >
                        rightTravelledLinks + rightFollowing.percentageTravelled * rightFollowing.linkOn.curve.curveLength)
                    {
                        if (rightFollower != null)
                        {
                            rightFollower.leftFollowing = null;
                        }
                        rightFollowing.leftFollowing = this;
                        if (directFollower && directFollower.rightFollowing == null)
                        {
                            directFollower.rightFollowing = rightFollowing;
                        }

                        if (rightFollowing.directFollowing &&
                            (!directFollowing ||
                            rightFollowing.directFollowing.isRightNeighborOf(directFollowing, out bool isBehind, out _) && isBehind))
                        {
                            rightFollowing = rightFollowing.directFollowing;
                        }
                        else
                        {
                            rightFollowing = null;
                        }

                    }

                }
                if (leftFollowing)
                {
                    float leftTravelledLinks = leftFollowing.linkOn.GetAncestorInfo(leftFollowing.laneOn + 1, out _, out _);

                    if (itravelledLinks + percentageTravelled * linkOn.curve.curveLength >
                        leftTravelledLinks + leftFollowing.percentageTravelled * leftFollowing.linkOn.curve.curveLength)
                    {
                        if (leftFollower != null)
                        {
                            leftFollower.rightFollowing = null;
                        }
                        leftFollowing.rightFollowing = this;
                        if (directFollower && directFollower.leftFollowing == null)
                        {
                            directFollower.leftFollowing = leftFollowing;
                        }
                        if (leftFollowing.directFollowing &&
                            (!directFollowing ||
                            leftFollowing.directFollowing.isLeftNeighborOf(directFollowing, out bool isBehind, out _) && isBehind))
                        {
                            leftFollowing = leftFollowing.directFollowing;
                        }
                        else
                        {
                            leftFollowing = null;
                        }
                    }

                }

                return true;
            }
        }


        // Logistic: Candidate go to Left/Right Lane -> Validate it's same as the caller's
        /* e.g.
         *  A  |  /   /
         *     | / B / 
         *     |/   /
         *          |
         *     |    |
         *        D |
         *  C  |    |
         *  A.isLeftNeighborOf(D) = True, false
         *  C.isLeftNeighborOf(B) = False, _
         *  D.isRightNeighborOf(A) = False, _
         *  B.isRightNeighbirOf(C) = True, false
         *  A.isLeftNeighborOf(B) = B.isRightNeighorOf(A) = False, _
         *  C.isLeftNeighborOf(D) = D.isRightNeighorOf(C) = True, true
         */

        public bool isLeftNeighborOf(VehicleLaneController rightCandiate, out bool isBehind, out float distanceBehind) 
        {
            if (rightCandiate.laneOn == rightCandiate.linkOn.minLane)
            {
                isBehind = false;
                distanceBehind = 0;
                return false;
            }
            float candiateProjectedLength = rightCandiate.linkOn.GetAncestorInfo(rightCandiate.laneOn - 1, out Node rightLeftOriginNode, out int rightLeftOriginLane);
            float myLength = linkOn.GetAncestorInfo(laneOn, out Node myOriginNode, out int myOriginLane);
            distanceBehind = candiateProjectedLength + rightCandiate.linkOn.curve.curveLength * rightCandiate.percentageTravelled - (myLength + linkOn.curve.curveLength * percentageTravelled);
            isBehind = distanceBehind > 0;
            return rightLeftOriginNode == myOriginNode && myOriginLane == rightLeftOriginLane;
        }

        public bool isRightNeighborOf(VehicleLaneController leftCandidate, out bool isBehind, out float distanceBehind)
        {
            if (leftCandidate.laneOn == leftCandidate.linkOn.maxLane)
            {
                isBehind = false;
                distanceBehind = 0;
                return false;
            }
            float candiateProjectedLength = leftCandidate.linkOn.GetAncestorInfo(leftCandidate.laneOn + 1, out Node leftRightOriginNode, out int leftRightOriginLane);
            float myLength = linkOn.GetAncestorInfo(laneOn, out Node myOriginNode, out int myOriginLane);
            distanceBehind = candiateProjectedLength + leftCandidate.linkOn.curve.curveLength * leftCandidate.percentageTravelled - (myLength + linkOn.curve.curveLength * percentageTravelled);
            isBehind = distanceBehind > 0;
            return leftRightOriginNode == myOriginNode && myOriginLane == leftRightOriginLane;
        }

        public float GetDistanceDirectlyBehind(VehicleLaneController direct)
        {
            return direct.linkOn.GetAncestorInfo(direct.laneOn, out _, out _) + direct.linkOn.curve.curveLength * direct.percentageTravelled -
                (linkOn.GetAncestorInfo(laneOn, out _, out _) + linkOn.curve.curveLength * percentageTravelled);
        }

        // Slow
        // Must be called at t = 0
        private static VehicleLaneController FindFrontVehicleInLane(Link link, int lane)
        {
            Link startLink = link;

            Link search = link;
            int searchLane = lane;

            int steps = 0;
            while (search.vehicles.Count == 0 || search.vehicles.TrueForAll(vh => vh.laneOn != searchLane))
            {
                if (!search.GetNextLink(searchLane, out search, out searchLane))
                {
                    return null;
                }
                // Loop Encountered
                if (steps > 0 && search == startLink)
                {
                    return null;
                }
                steps++;
            }

            var fronts = search.vehicles.Where(vh => vh.laneOn == searchLane);
            if (fronts.ToList().Count == 0) return null;
            return fronts.MinBy(vh => vh.percentageTravelled);
            
        }

        private VehicleLaneController GetFollowingForNewRightLane()
        {
            return _GetFollowingForNewNeighborLane(true);
        }

        private VehicleLaneController GetFollowingForNewLeftLane()
        {
            return _GetFollowingForNewNeighborLane(false);
        }

        // Must be Called at t = 0
        // Locate right FRONT neighbor and update info for all affected vehicles, don't care about right BACK
        // By the time this function is called, directFollowing & directFollower must already be set
        private VehicleLaneController _GetFollowingForNewNeighborLane(bool right)
        {
            Link startLink = linkOn;

            Link search = linkOn;
            int searchLane = laneOn;
            int searchNeighborLane = right ? laneOn + 1: laneOn - 1;
            while (true)
            {
                if (directFollowing != null && search == directFollowing.linkOn)
                {
                    var candidates = search.vehicles.Where(vh => vh.laneOn == searchNeighborLane && vh.percentageTravelled < directFollowing.percentageTravelled);
                    if (candidates.ToList().Count > 0)
                    {
                        return candidates.MinBy(cand => cand.percentageTravelled);
                    }
                    else
                    {
                        return null;
                    }
                }

                if (search.vehicles.Count > 0 && search.vehicles.Any(vh => vh.laneOn == searchNeighborLane))
                {
                    var neighborLaneGroup = search.vehicles.Where(vh => vh.laneOn == searchNeighborLane);
                    return neighborLaneGroup.MinBy(vh => vh.percentageTravelled);
                }

                Link tmp = search;
                if (!tmp.GetNextLink(searchLane, out tmp, out searchLane))
                {
                    return null;
                }
                Link tmp_neighbor = search;
                if (!tmp_neighbor.GetNextLink(searchNeighborLane, out tmp_neighbor, out searchNeighborLane))
                {
                    return null;
                }
                if (tmp != tmp_neighbor)
                {
                    // Right lane diverts
                    return null;
                }
                search = tmp;

                if (search == startLink)
                {
                    // loop
                    return null;
                }
            }
        }

        // searchNeighborLane must be aligned with searchLane during throughout the search
        // If searchLane == searchNeighborLane then alignment is not taken into consideration
        // Slow
        private static VehicleLaneController _SearchFollowing(Link startLink, int searchLane, int searchNeighborLane, float percentageTravelled)
        {
            Link search = startLink;
            var trivial = search.vehicles.Where(vh => vh.percentageTravelled > percentageTravelled && vh.laneOn == searchLane);
            if (trivial.ToList().Count > 0)
            {
                return trivial.MinBy(vh => vh.percentageTravelled);
            }
            while (true)
            {
                Link tmp = search;
                if (!tmp.GetNextLink(searchLane, out tmp, out searchLane))
                {
                    return null;
                }
                Link tmp_neighbor = search;
                if (!tmp_neighbor.GetNextLink(searchNeighborLane, out tmp_neighbor, out searchNeighborLane))
                {
                    return null;
                }
                if (tmp != tmp_neighbor)
                {
                    // Neighbor lane diverts
                    return null;
                }
                search = tmp;

                if (search == startLink)
                {
                    // loop
                    return null;
                }

                var candidates = search.vehicles.Where(vh => vh.laneOn == searchLane);
                {
                    if (candidates.ToList().Count > 0)
                    {
                        return candidates.MinBy(vh => vh.percentageTravelled);
                    }
                }
            }
        }

        // searchNeighborLane must be aligned with searchLane during throughout the search
        // If searchLane == searchNeighborLane then alignment is not taken into consideration
        // Slow
        private static VehicleLaneController _SearchFollower(Link startLink, int searchLane, int searchNeighborLane, float percentageTravelled)
        {
            Link search = startLink;
            var trivial = search.vehicles.Where(vh => vh.percentageTravelled < percentageTravelled && vh.laneOn == searchLane);
            if (trivial.ToList().Count > 0)
            {
                return trivial.MaxBy(vh => vh.percentageTravelled);
            }
            while (true)
            {
                Link tmp = search;
                if (!tmp.GetPreviousLink(searchLane, out tmp, out searchLane))
                {
                    return null;
                }
                Link tmp_neighbor = search;
                if (!tmp_neighbor.GetPreviousLink(searchNeighborLane, out tmp_neighbor, out searchNeighborLane))
                {
                    return null;
                }
                if (tmp != tmp_neighbor)
                {
                    // Neighbor lane diverts
                    return null;
                }
                search = tmp;

                if (search == startLink)
                {
                    // loop
                    return null;
                }

                var candidates = search.vehicles.Where(vh => vh.laneOn == searchLane);
                {
                    if (candidates.ToList().Count > 0)
                    {
                        return candidates.MaxBy(vh => vh.percentageTravelled);
                    }
                }
            }
        }

        void OnDrawGizmosSelected(){
            if (directFollowing != null){
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, directFollowing.transform.position);
            }

            if (rightFollowing != null){
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, rightFollowing.transform.position);
            }

            if (leftFollowing != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, leftFollowing.transform.position);
            }
        }
    }
}
