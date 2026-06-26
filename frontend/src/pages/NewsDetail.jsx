import { useParams, Link } from "react-router-dom";
import "./NewsDetail.css";
import { newsData } from "../data/news";

export default function NewsDetail() {
  const { id } = useParams();

  const article = newsData.find((n) => n.id === id);

  if (!article) return <div>Not found</div>;

  return (
    <div className="news-detail">
      <Link to="/news" className="back">← Back</Link>

      <img src={article.image} className="hero" />

      <div className="content">
        <span className="tag">{article.category}</span>
        <h1>{article.title}</h1>

        <div className="meta">
          <span>{article.author}</span>
          <span>{article.date}</span>
          <span>{article.readTime}</span>
        </div>

        <p>{article.content}</p>
      </div>
    </div>
  );
}