import { Link } from "react-router-dom";
import "./NewsList.css";
import { newsData } from "../data/news";
import { useMemo, useState } from "react";

export default function NewsList() {
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("ALL");

  // lấy danh sách category duy nhất
  const categories = useMemo(() => {
    return ["ALL", ...new Set(newsData.map((item) => item.category))];
  }, []);

  // filter data
  const filteredData = useMemo(() => {
    return newsData.filter((item) => {
      const matchCategory =
        category === "ALL" || item.category === category;

      const matchSearch =
        item.title.toLowerCase().includes(search.toLowerCase()) ||
        item.desc.toLowerCase().includes(search.toLowerCase());

      return matchCategory && matchSearch;
    });
  }, [search, category]);

  return (
    <div className="news-page">
      <div className="news-header">
        <h1>News & Features</h1>
        <p>Explore what makes DevPrep AI the smartest way to prepare.</p>

        {/* SEARCH + FILTER */}
      <div className="news-controls">
  <input
    type="text"
    placeholder="Search news..."
    value={search}
    onChange={(e) => setSearch(e.target.value)}
  />

  <div className="category-filter">
    {categories.map((cat) => (
      <button
        key={cat}
        className={`category-btn ${category === cat ? "active" : ""}`}
        onClick={() => setCategory(cat)}
      >
        {cat}
      </button>
    ))}
  </div>
</div>
      </div>

      <div className="news-grid">
        {filteredData.length > 0 ? (
          filteredData.map((item) => (
            <Link to={`/news/${item.id}`} className="news-card" key={item.id}>
              <img src={item.image} alt="" />

              <div className="news-content">
                <span className="tag">{item.category}</span>
                <h2>{item.title}</h2>
                <p>{item.desc}</p>

                <div className="meta">
                  <span>{item.author}</span>
                  <span>{item.date}</span>
                  <span>{item.readTime}</span>
                </div>
              </div>
            </Link>
          ))
        ) : (
          <p style={{ textAlign: "center", gridColumn: "1/-1" }}>
            No results found
          </p>
        )}
      </div>
    </div>
  );
}