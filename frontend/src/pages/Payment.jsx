import { useState } from "react";
import "./Payment.css";
import useAuthStore from "../store/useAuthStore";

export default function Payment() {
  const [active, setActive] = useState(0);
  const accessToken = useAuthStore((state) => state.accessToken);

  const handleSelectPlan = (plan) => {
    if (!accessToken) {
      alert("Please login first");
      return;
    }

    console.log("Selected plan:", plan);
  };

  const plans = [
    {
      title: "Starter",
      credits: "10 Credits",
      price: "$29",
      subtitle: "$2.90 per credit",
      features: [
        "5 Full AI Mock Interviews",
        "Standard Feedback",
        "30-Day Expiry",
      ],
      popular: false,
    },
    {
      title: "Professional",
      credits: "100 Credits",
      oldPrice: "$290",
      price: "$199",
      subtitle: "$1.99/credit",
      save: "Save 31%",
      features: [
        "50 Mock Interviews",
        "Advanced Code Analysis",
        "Priority Processing",
        "No Expiry",
      ],
      popular: true,
    },
    {
      title: "Enterprise Ready",
      credits: "500 Credits",
      oldPrice: "$1,450",
      price: "$749",
      subtitle: "$1.49/credit",
      save: "Save 48%",
      features: [
        "Unlimited Interviews",
        "Learning Roadmap",
        "1-on-1 Human Review",
        "Concierge Support",
      ],
      popular: false,
    },
  ];

  const faqs = [
    {
      q: "How do credits work?",
      a: "Each credit allows you to conduct one AI mock interview including technical analysis and behavioral feedback.",
    },
    {
      q: "Can I upgrade my plan later?",
      a: "Yes. You only need to pay the difference.",
    },
    {
      q: "Is the payment secure?",
      a: "Yes. All transactions are encrypted with SSL.",
    },
    {
      q: "Do credits ever expire?",
      a: "Starter expires after 30 days. Professional and Enterprise never expire.",
    },
  ];

  return (
    <div className="payment-page">

      <div className="payment-header">
        <h1>Choose Your Plan</h1>
        <p>Pick the credit package that fits your interview prep goals.</p>
      </div>

      <div className="plans">
        {plans.map((plan, index) => (
          <div
            key={index}
            className={`plan-card ${plan.popular ? "popular" : ""}`}
          >
            {plan.popular && (
              <div className="popular-tag">MOST POPULAR</div>
            )}

            <span className="badge">{plan.title}</span>

            <h2>{plan.credits}</h2>

            {plan.oldPrice && (
              <div className="old-price">{plan.oldPrice}</div>
            )}

            <div className="price">{plan.price}</div>

            <div className="save-row">
              {plan.save && <span className="save">{plan.save}</span>}
              <span>{plan.subtitle}</span>
            </div>

            <ul>
              {plan.features.map((item, i) => (
                <li key={i}>✔ {item}</li>
              ))}
            </ul>

            <button onClick={() => handleSelectPlan(plan)}>
              Select Plan
            </button>
          </div>
        ))}
      </div>

      <div className="faq">
        <h2>Frequently Asked Questions</h2>

        {faqs.map((item, index) => (
          <div className="faq-item" key={index}>
            <div
              className="faq-question"
              onClick={() =>
                setActive(active === index ? -1 : index)
              }
            >
              <span>{item.q}</span>
              <span>{active === index ? "-" : "+"}</span>
            </div>

            {active === index && (
              <div className="faq-answer">{item.a}</div>
            )}
          </div>
        ))}
      </div>

      <div className="secure">
        <div className="icons">💳 🏦 💵 🔒</div>
        <p>Secure 256-bit SSL encrypted checkout</p>
      </div>

    </div>
  );
}