import React from "react";
import { MdSearch, MdFilterList, MdCalendarToday, MdDownload, MdRefresh, MdHelpOutline } from "react-icons/md";

function TransactionView() {
  const txns = [
    { id: "#TXN-88421", date: "Oct 24, 2023", desc: "100 Credit Bundle", amount: "100cr", status: "COMPLETED" },
    { id: "#TXN-88319", date: "Oct 20, 2023", desc: "10 Credit Bundle", amount: "10cr", status: "COMPLETED" },
    { id: "#TXN-87982", date: "Oct 15, 2023", desc: "500 Credit Bundle", amount: "500cr", status: "COMPLETED" },
    { id: "#TXN-87654", date: "Oct 10, 2023", desc: "Professional Tier Subscription", amount: "$49.00", status: "FAILED" },
    { id: "#TXN-87422", date: "Oct 08, 2023", desc: "Starter Plan", amount: "$19.00", status: "PENDING" },
  ];

  return (
    <div className="tab-view-container">
      <div className="view-title">
        <h1>Transaction History</h1>
        <p>Monitor your billing history, credit purchases, and active subscriptions.</p>
      </div>

      {/* Metric Counters */}
      <div className="metrics-row">
        <div className="metric-box">
          <span>CURRENT BALANCE</span>
          <h2>350 <span>CR</span></h2>
        </div>
        <div className="metric-box">
          <span>TOTAL PURCHASED</span>
          <h2 className="cyan-text">550 <span>CR</span></h2>
        </div>
        <div className="metric-box">
          <span>SESSIONS REMAINING</span>
          <h2>12 <span className="sub-label">Active</span></h2>
        </div>
      </div>

      {/* Filter and Table Section */}
      <div className="table-wrapper-card">
        <div className="table-filter-bar">
          <div className="search-input-wrapper">
            <MdSearch className="search-icon" />
            <input type="text" placeholder="Search transactions..." />
          </div>
          <button className="btn-filter"><MdFilterList /> Filters</button>
          <button className="btn-filter"><MdCalendarToday /> Last 30 Days</button>
          <button className="btn-buy-credits">+ Buy Credits</button>
        </div>

        <table className="txn-table">
          <thead>
            <tr>
              <th>TRANSACTION ID</th>
              <th>DATE</th>
              <th>DESCRIPTION</th>
              <th>AMOUNT</th>
              <th>STATUS</th>
              <th>ACTION</th>
            </tr>
          </thead>
          <tbody>
            {txns.map((t, idx) => (
              <tr key={idx}>
                <td className="txn-id">{t.id}</td>
                <td>{t.date}</td>
                <td>{t.desc}</td>
                <td>{t.amount}</td>
                <td>
                  <span className={`status-tag ${t.status.toLowerCase()}`}>{t.status}</span>
                </td>
                <td>
                  {t.status === "FAILED" ? <MdRefresh className="action-ic" /> : <MdDownload className="action-ic" />}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Pagination */}
        <div className="table-pagination">
          <span>Showing 1 to 5 of 24 results</span>
          <div className="page-btns">
            <button className="disabled">‹</button>
            <button className="active">1</button>
            <button>2</button>
            <button>3</button>
            <span>...</span>
            <button>5</button>
            <button>›</button>
          </div>
        </div>
      </div>

      {/* Support Box */}
      <div className="support-banner">
        <div className="support-info">
          <MdHelpOutline size={22} className="sup-icon" />
          <div>
            <h4>Need help with a transaction?</h4>
            <p>Contact our billing support team for assistance with failed payments or refund requests.</p>
          </div>
        </div>
        <button className="btn-support-center">Support Center</button>
      </div>
    </div>
  );
}
export default TransactionView;